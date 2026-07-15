using System.Text;

namespace AppBender.Core.Services;

/// <summary>
/// Lightweight GraphQL-style executor over Data Hub entities (no external dependency).
/// Queries:   { customers(top: 10, search: "jo", sortBy: "name", desc: true, filter: "status eq Active") { id name email } }
/// Mutations: mutation { create_customers(data: { name: "Jo" }) { id }
///                       update_customers(id: "abc", data: { name: "Jo" }) { id }
///                       delete_customers(id: "abc") { id } }
/// An empty selection set (or "*") returns every field.
/// </summary>
public class GraphQlExecutor(IDataHubService dataHub)
{
    public async Task<Dictionary<string, object?>> ExecuteAsync(string query)
    {
        try
        {
            var parser = new Parser(query);
            var doc = parser.ParseDocument();
            var data = new Dictionary<string, object?>();
            foreach (var field in doc.Fields)
                data[field.Alias ?? field.Name] = await ExecuteFieldAsync(field, doc.IsMutation);
            return new Dictionary<string, object?> { ["data"] = data };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["errors"] = new List<object?> { new Dictionary<string, object?> { ["message"] = ex.Message } }
            };
        }
    }

    private async Task<object?> ExecuteFieldAsync(FieldNode field, bool isMutation)
    {
        if (isMutation)
        {
            if (field.Name.StartsWith("create_"))
            {
                var entity = field.Name["create_".Length..];
                var data = field.Args.TryGetValue("data", out var d) && d is Dictionary<string, object?> dict
                    ? dict : throw new InvalidOperationException($"'{field.Name}' requires a 'data' argument.");
                var record = await dataHub.CreateRecordAsync(entity, data);
                return Shape(record, field.Selection);
            }
            if (field.Name.StartsWith("update_"))
            {
                var entity = field.Name["update_".Length..];
                var id = field.Args.TryGetValue("id", out var i) ? i?.ToString() : null;
                var data = field.Args.TryGetValue("data", out var d) && d is Dictionary<string, object?> dict ? dict : null;
                if (id is null || data is null) throw new InvalidOperationException($"'{field.Name}' requires 'id' and 'data'.");
                var record = await dataHub.UpdateRecordAsync(entity, id, data);
                return Shape(record, field.Selection);
            }
            if (field.Name.StartsWith("delete_"))
            {
                var entity = field.Name["delete_".Length..];
                var id = (field.Args.TryGetValue("id", out var i) ? i?.ToString() : null)
                    ?? throw new InvalidOperationException($"'{field.Name}' requires 'id'.");
                await dataHub.DeleteRecordAsync(entity, id);
                return new Dictionary<string, object?> { ["id"] = id, ["deleted"] = true };
            }
            throw new InvalidOperationException($"Unknown mutation '{field.Name}'. Use create_/update_/delete_ + entity name.");
        }

        // Query: field name is the entity name; optional id arg fetches one record.
        if (field.Args.TryGetValue("id", out var idArg) && idArg is not null)
        {
            var record = await dataHub.GetRecordAsync(field.Name, idArg.ToString()!);
            return record is null ? null : Shape(record, field.Selection);
        }

        var options = new QueryOptions
        {
            PageSize = field.Args.TryGetValue("top", out var top) ? Convert.ToInt32(top) : 50,
            Page = field.Args.TryGetValue("page", out var page) ? Convert.ToInt32(page) : 1,
            Search = field.Args.TryGetValue("search", out var s) ? s?.ToString() : null,
            SortBy = field.Args.TryGetValue("sortBy", out var sb) ? sb?.ToString() : null,
            SortDesc = field.Args.TryGetValue("desc", out var dsc) && dsc is bool b && b
        };
        if (field.Args.TryGetValue("filter", out var flt) && flt is string filterText)
        {
            // "field op value" triplets separated by " and "
            foreach (var clause in filterText.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = clause.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    options.Filters.Add(new FieldFilter { Field = parts[0], Op = parts[1], Value = parts.Length > 2 ? parts[2].Trim('"', '\'') : null });
            }
        }

        var result = await dataHub.QueryAsync(field.Name, options);
        return result.Records.Select(r => Shape(r, field.Selection)).ToList();
    }

    private static Dictionary<string, object?> Shape(Models.DataRecord record, List<string> selection)
    {
        var data = record.Data;
        var shaped = new Dictionary<string, object?>();
        if (selection.Count == 0 || selection.Contains("*"))
        {
            shaped["id"] = record.Id;
            foreach (var (k, v) in data) shaped[k] = v;
            shaped["createdAt"] = record.CreatedAt;
            shaped["updatedAt"] = record.UpdatedAt;
            return shaped;
        }
        foreach (var name in selection)
        {
            shaped[name] = name switch
            {
                "id" => record.Id,
                "createdAt" => record.CreatedAt,
                "updatedAt" => record.UpdatedAt,
                _ => data.TryGetValue(name, out var v) ? v : null
            };
        }
        return shaped;
    }

    // ---------------------------------------------------------------- parser

    private record FieldNode(string Name, string? Alias, Dictionary<string, object?> Args, List<string> Selection);
    private record Document(bool IsMutation, List<FieldNode> Fields);

    private class Parser(string text)
    {
        private int _pos;

        public Document ParseDocument()
        {
            SkipWs();
            var isMutation = false;
            if (PeekWord() is "query" or "mutation")
            {
                isMutation = ReadWord() == "mutation";
                SkipWs();
                if (_pos < text.Length && (char.IsLetter(text[_pos]) || text[_pos] == '_')) ReadWord(); // operation name
                SkipWs();
            }
            Expect('{');
            var fields = new List<FieldNode>();
            SkipWs();
            while (_pos < text.Length && text[_pos] != '}')
            {
                fields.Add(ParseField());
                SkipWs();
                if (_pos < text.Length && text[_pos] == ',') { _pos++; SkipWs(); }
            }
            Expect('}');
            if (fields.Count == 0) throw new FormatException("Empty selection.");
            return new Document(isMutation, fields);
        }

        private FieldNode ParseField()
        {
            var name = ReadWord();
            string? alias = null;
            SkipWs();
            if (_pos < text.Length && text[_pos] == ':')
            {
                _pos++; SkipWs();
                alias = name;
                name = ReadWord();
                SkipWs();
            }
            var args = new Dictionary<string, object?>();
            if (_pos < text.Length && text[_pos] == '(')
            {
                _pos++;
                SkipWs();
                while (_pos < text.Length && text[_pos] != ')')
                {
                    var argName = ReadWord();
                    SkipWs(); Expect(':'); SkipWs();
                    args[argName] = ParseValue();
                    SkipWs();
                    if (_pos < text.Length && text[_pos] == ',') { _pos++; SkipWs(); }
                }
                Expect(')');
                SkipWs();
            }
            var selection = new List<string>();
            if (_pos < text.Length && text[_pos] == '{')
            {
                _pos++;
                SkipWs();
                while (_pos < text.Length && text[_pos] != '}')
                {
                    selection.Add(text[_pos] == '*' ? ReadStar() : ReadWord());
                    SkipWs();
                    if (_pos < text.Length && text[_pos] == ',') { _pos++; SkipWs(); }
                }
                Expect('}');
            }
            return new FieldNode(name, alias, args, selection);
        }

        private object? ParseValue()
        {
            SkipWs();
            var c = text[_pos];
            if (c == '"') return ReadString();
            if (c == '{')
            {
                _pos++;
                var obj = new Dictionary<string, object?>();
                SkipWs();
                while (_pos < text.Length && text[_pos] != '}')
                {
                    var key = text[_pos] == '"' ? ReadString() : ReadWord();
                    SkipWs(); Expect(':'); SkipWs();
                    obj[key] = ParseValue();
                    SkipWs();
                    if (_pos < text.Length && text[_pos] == ',') { _pos++; SkipWs(); }
                }
                Expect('}');
                return obj;
            }
            if (c == '[')
            {
                _pos++;
                var list = new List<object?>();
                SkipWs();
                while (_pos < text.Length && text[_pos] != ']')
                {
                    list.Add(ParseValue());
                    SkipWs();
                    if (_pos < text.Length && text[_pos] == ',') { _pos++; SkipWs(); }
                }
                Expect(']');
                return list;
            }
            if (char.IsDigit(c) || c == '-')
            {
                var start = _pos;
                while (_pos < text.Length && (char.IsDigit(text[_pos]) || text[_pos] is '.' or '-' or 'e' or 'E' or '+')) _pos++;
                var raw = text[start.._pos];
                return raw.Contains('.') || raw.Contains('e') || raw.Contains('E')
                    ? double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture)
                    : long.Parse(raw);
            }
            var word = ReadWord();
            return word switch { "true" => true, "false" => false, "null" => null, _ => word };
        }

        private string ReadString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (_pos < text.Length && text[_pos] != '"')
            {
                if (text[_pos] == '\\' && _pos + 1 < text.Length)
                {
                    _pos++;
                    sb.Append(text[_pos] switch { 'n' => '\n', 't' => '\t', 'r' => '\r', var e => e });
                }
                else sb.Append(text[_pos]);
                _pos++;
            }
            Expect('"');
            return sb.ToString();
        }

        private string ReadWord()
        {
            SkipWs();
            var start = _pos;
            while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] == '_')) _pos++;
            if (start == _pos) throw new FormatException($"Expected name at position {_pos}.");
            return text[start.._pos];
        }

        private string ReadStar() { _pos++; return "*"; }
        private string? PeekWord()
        {
            var save = _pos;
            try { return ReadWord(); }
            catch { return null; }
            finally { _pos = save; }
        }

        private void Expect(char c)
        {
            SkipWs();
            if (_pos >= text.Length || text[_pos] != c)
                throw new FormatException($"Expected '{c}' at position {_pos}.");
            _pos++;
        }

        private void SkipWs()
        {
            while (_pos < text.Length && (char.IsWhiteSpace(text[_pos]) || text[_pos] == '#'))
            {
                if (text[_pos] == '#') { while (_pos < text.Length && text[_pos] != '\n') _pos++; }
                else _pos++;
            }
        }
    }
}

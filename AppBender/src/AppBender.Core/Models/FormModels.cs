using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

/// <summary>Component types available in the visual form designer palette.</summary>
public static class ComponentTypes
{
    public const string TextBox = "textbox";
    public const string TextArea = "textarea";
    public const string RichText = "richtext";
    public const string Number = "number";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Url = "url";
    public const string Password = "password";
    public const string Date = "date";
    public const string Time = "time";
    public const string DateTimeLocal = "datetime";
    public const string Dropdown = "dropdown";
    public const string Radio = "radio";
    public const string Checkbox = "checkbox";
    public const string CheckboxList = "checkboxlist";
    public const string Toggle = "toggle";
    public const string Slider = "slider";
    public const string Rating = "rating";
    public const string FileUpload = "fileupload";
    public const string ImageUpload = "imageupload";
    public const string Lookup = "lookup";
    public const string Label = "label";
    public const string Heading = "heading";
    public const string Paragraph = "paragraph";
    public const string Divider = "divider";
    public const string Spacer = "spacer";
    public const string Image = "image";
    public const string Hyperlink = "hyperlink";
    public const string Button = "button";
    public const string Section = "section";
    public const string Columns = "columns";
    public const string DataGrid = "datagrid";
    public const string Chart = "chart";
    public const string Html = "html";
    /// <summary>Live computed value from other fields, e.g. expression "qty * price".</summary>
    public const string Calc = "calc";

    public static readonly (string Type, string Label, string Icon, string Category)[] Palette =
    [
        (TextBox, "Text Box", "🔤", "Input"),
        (TextArea, "Text Area", "📝", "Input"),
        (RichText, "Rich Text", "🖋️", "Input"),
        (Number, "Number", "🔢", "Input"),
        (Email, "Email", "📧", "Input"),
        (Phone, "Phone", "📞", "Input"),
        (Url, "URL", "🔗", "Input"),
        (Password, "Password", "🔒", "Input"),
        (Date, "Date", "📅", "Input"),
        (Time, "Time", "⏰", "Input"),
        (DateTimeLocal, "Date & Time", "🗓️", "Input"),
        (Dropdown, "Dropdown", "▾", "Choice"),
        (Radio, "Radio Group", "🔘", "Choice"),
        (Checkbox, "Checkbox", "☑️", "Choice"),
        (CheckboxList, "Checkbox List", "📋", "Choice"),
        (Toggle, "Toggle", "🎚️", "Choice"),
        (Slider, "Slider", "🎛️", "Choice"),
        (Rating, "Rating", "⭐", "Choice"),
        (Lookup, "Lookup", "🔍", "Data"),
        (DataGrid, "Data Grid", "🗃️", "Data"),
        (Chart, "Chart", "📊", "Data"),
        (Calc, "Calculated Value", "🟰", "Data"),
        (FileUpload, "File Upload", "📎", "Media"),
        (ImageUpload, "Image Upload", "🖼️", "Media"),
        (Image, "Image", "🏞️", "Media"),
        (Label, "Label", "🏷️", "Layout"),
        (Heading, "Heading", "🔠", "Layout"),
        (Paragraph, "Paragraph", "¶", "Layout"),
        (Hyperlink, "Hyperlink", "🌐", "Layout"),
        (Divider, "Divider", "―", "Layout"),
        (Spacer, "Spacer", "⬜", "Layout"),
        (Section, "Section", "🗂️", "Layout"),
        (Columns, "Columns", "🏛️", "Layout"),
        (Html, "HTML Block", "</>", "Layout"),
        (Button, "Button", "🔲", "Action"),
    ];
}

/// <summary>A node in a form layout tree (serialized to FormDefinition.LayoutJson).</summary>
public class FormComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Type { get; set; } = ComponentTypes.TextBox;
    public string Label { get; set; } = "";
    /// <summary>Bound Data Hub field name (for input components).</summary>
    public string? Field { get; set; }
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
    public string? HelpText { get; set; }
    public bool Required { get; set; }
    public bool ReadOnly { get; set; }
    public bool Visible { get; set; } = true;
    /// <summary>Grid width 1..12.</summary>
    public int Width { get; set; } = 12;
    /// <summary>Extra type-specific properties (options, entity, chartType, url, html, action...).</summary>
    public Dictionary<string, string> Props { get; set; } = [];
    public List<FormComponent> Children { get; set; } = [];

    public string Prop(string key, string fallback = "") =>
        Props.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
}

public class FormDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    [MaxLength(80)] public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Icon { get; set; } = "📄";
    /// <summary>Bound Data Hub entity name (optional).</summary>
    public string? EntityName { get; set; }
    public string LayoutJson { get; set; } = "[]";
    /// <summary>Workflow to trigger on submit (optional).</summary>
    public string? SubmitWorkflowId { get; set; }
    public string SubmitLabel { get; set; } = "Submit";
    public bool IsPublished { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<FormComponent> Layout
    {
        get => JsonUtil.DeserializeOrNew<List<FormComponent>>(LayoutJson);
        set => LayoutJson = JsonUtil.Serialize(value);
    }
}

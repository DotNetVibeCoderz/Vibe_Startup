namespace Joka;

/// <summary>
/// Marker type for the shared string table. `IStringLocalizer&lt;SharedResource&gt;`
/// resolves to Resources/SharedResource.*.resx.
///
/// The neutral file holds Indonesian, which is the product's default language.
/// A key with no English translation therefore falls back to Indonesian rather
/// than rendering the raw key - a page that is not translated yet degrades to
/// the original copy instead of breaking.
/// </summary>
public class SharedResource
{
}

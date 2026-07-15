# Forms

## Designer (`/forms/designer`)

Three panels:

1. **Palette** — 30+ components in categories *Input, Choice, Data, Media, Layout, Action*.
   Click or drag onto the canvas.
2. **Canvas** — 12-column grid. Click a component to select; hover for tools
   (↑ ↓ move, ⧉ duplicate, ✕ remove). A **live preview** renders below the canvas.
3. **Properties** — label, entity field binding, width (1–12), placeholder, default,
   help text, required/read-only/visible, plus type-specific props
   (options, bound entity, chart settings, URL, HTML).

## Component highlights

- **Lookup** — dropdown of records from another entity (`entity` + `displayField` props).
- **Data Grid** — table of an entity's records with `columns`, `pageSize`, and a `filter`
  ("field op value", `and`-joined). The filter may reference other form fields with
  `{{fieldName}}` — this powers **master-detail** forms: a grid with
  `customer_id eq {{customer_id}}` reloads whenever the Customer lookup changes.
- **Chart** — bar/pie aggregation over an entity (`groupBy`, optional `valueField`,
  same dynamic `filter` support as grids).
- **Calculated Value** — live computed number from other fields
  (`expression` prop, e.g. `qty * unit_price` or
  `if(gte(total, nisab), total * 0.025, 0)`; formats: number/currency/percent, prefix/suffix).
  See the seeded *Kalkulator Cicilan* and *Hitung Zakat Mal* forms.
- **Section / Columns** — nested layout containers.
- **HTML block** — raw HTML for custom content.

Seeded showcase forms: **Sales Dashboard** (charts + grid), **Orders Grid + Filter**,
**Customer Orders (Master-Detail)**, **Kalkulator Cicilan**, **Hitung Zakat Mal**.

## Entity binding & submit

Bind the form to a Data Hub entity and map components to fields; submitting creates a record
(with full schema validation). Optionally pick an **on-submit workflow**; it receives
`{{trigger.values}}`, `{{trigger.recordId}}`, `{{trigger.formId}}`.

A `FormSubmitted` trigger type also exists on workflows — it fires for *any* submission of the
configured form (or all forms), independent of the form's own submit workflow.

## Running forms

- **Designer preview** — always live.
- **`/forms/run/{slug}`** — standalone fill page for authenticated users.
- **Published apps** — add the form to an app (`/apps`) and share `/a/{app-slug}`.

## Export / import

Use the ⭳ button on the forms list for a single form, or **Import/Export** for the whole
workspace. Forms are plain JSON (`FormDefinition` with a `FormComponent` tree) — editable by hand
or by AI.

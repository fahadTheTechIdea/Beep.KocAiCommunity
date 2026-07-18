# MudBlazor local reference

Purpose: use this folder as the **first stop** for correct MudBlazor **syntax, parameters, and patterns** before editing `.razor` files in this repo. Each `*.txt` file mirrors documentation-style content for one component or topic.

## Official documentation (always current)

- Overview: [https://mudblazor.com/getting-started/installation](https://mudblazor.com/getting-started/installation)
- Components index: [https://mudblazor.com/docs/components](https://mudblazor.com/docs/components)
- API reference (generated): [https://mudblazor.com/api](https://mudblazor.com/api)

Use **official docs** when verifying behavior for the **exact MudBlazor version** referenced in your `.csproj` (e.g. `MudBlazor` 8.x).

## Required workflow

1. Before changing markup for **MudButton**, **MudForm**, **MudTable**, **MudDialog**, layout, theme, or providers — open the matching **`ComponentName.txt`** in this folder when it exists.
2. Prefer **parameter names and enums** from local reference + official docs over guessing (`Color.*`, `Variant.*`, `Dense`, etc.).
3. For shell/theme/navigation work, read **`Layouts.txt`**, **`Theme.txt`**, **`Services.txt`**, **`AppBar.txt`**, **`Drawer.txt`** first.

## Projects in this repo that use MudBlazor

- **`Beep.EventsRegistration`** — Blazor Server + MudBlazor + RTL/localization; follow this reference for catalog, forms, dialogs, DataGrid, Stepper (wizard), certificate gallery, etc.
- **`TheTechIdeaWeb.Web.TemplateApp`** — reference shell patterns.
- Other MudBlazor apps under `TheTechIdeaWeb` — same rules.

## Read by UI need

### Navigation and workflow

- `NavMenu.txt`, `Menus.txt`, `Tabs.txt`, `Stepper.txt`, `Tree.txt`, `BreadCrumbs.txt`

### Forms and actions

- `Form.txt`, `Field.txt`, `Button.txt`, `TextField.txt`, `NumericField.txt`, `Select.txt`, `DatePicker.txt`, `DateRangePicker.txt`, `TimePicker.txt`, `CheckBox.txt`, `Radio.txt`, `AutoComplete.txt`, `FileUpload.txt`, `Slider.txt`

### Layout and structure

- `Layouts.txt`, `Theme.txt`, `Services.txt`, `Container.txt`, `Grid.txt`, `Card.txt`, `Drawer.txt`, `AppBar.txt`, `PopOver.txt`

### Data-heavy pages

- `DataGrid.txt`, `DropZone.txt`, `Chips.txt`, `ChipSet.txt`, `Badge.txt`

### Feedback

- `Alert.txt`, `Dialog.txt`, `MessageBox.txt`, `Progress.txt`, `Avatar.txt`

### Theme and infrastructure

- `Localization.txt`, `ParameterState.txt`

### File index

See this directory listing: one `.txt` per component/topic (e.g. `Button.txt`, `Dialog.txt`, …).

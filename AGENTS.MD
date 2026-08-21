---
description: |
  Coding and formatting rules for PowerShell and C#, tailored for use with GitHub Copilot Chat, Cursor, and Visual Studio Code.
  Follows modular, performance-aware, and documentation-friendly practices.
globs:
  - "**/*.ps1"
  - "**/*.psm1"
  - "**/*.cs"
alwaysApply: true
---

# 📏 General Rules

- Prefer smaller files over large ones.
- Split large classes using `partial class` where applicable.
- Always follow `.editorconfig` rules if available.
- All rendered tables must use triple backticks (` ``` `) with **plain-text layout** inside, not markdown-style tables in chat.
  ```
  +----+------------+-----------+
  | ID | Name       | Role      |
  +----+------------+-----------+
  | 1  | Alice      | Admin     |
  | 2  | Bob        | User      |
  +----+------------+-----------+
  ```
- They should be rendered as markdown tables in markdown files
---

# ⚙️ PowerShell Rules

## 📁 Structure

- One function per file.
- Place public functions in `Public\`, private in `Private\` folder.
- Avoid appending to arrays with `+=`; use `[array] $x = foreach { ... }` or generic lists.
- Prefer object construction over `Add-Member`.

## 🧠 Syntax

- Use **K&R/OTBS** style for braces.
- Always use **full parameter names** in scripts.
- Avoid `Where-Object`, `Measure-Object` inside loops for performance — use `foreach`, `.Count`.
- Avoid `return` unless required; implicit return is sufficient.
- No use of PowerShell 7–specific syntax unless explicitly required.
- Always support **PowerShell 5.1 and 7.0+** compatibility.

## 🧾 Function Header Documentation Format

Use PowerShell comment-based help **inside** the function, below `function` and above `[CmdletBinding()]`:

```powershell
function New-MyApp {
    <#
    .SYNOPSIS
    Creates or updates an Azure AD application and adds credentials.

    .DESCRIPTION
    Creates or updates an application if it doesn't exist and adds credentials.

    .PARAMETER ApplicationName
    The display name of the application.

    .EXAMPLE
    New-MyApp -ApplicationName "MyAPI"

    .NOTES
    Requires Microsoft.Graph.Applications module and Application.ReadWrite.All permissions.
    #>
    [CmdletBinding()]
    param (...)
    ...
}
```

---

# 💬 GitHub Copilot Chat Rules

## 🔧 Code Generation

```jsonc
"github.copilot.chat.codeGeneration.instructions": [
  { "text": "For PowerShell always use K&R/OTBS style." },
  { "text": "For PowerShell always add documentation for functions inside the function block." },
  { "text": "For PowerShell do not use PowerShell 7 specific features unless specified. Always make sure PS 5.1 and PS 7 compatibility." },
  { "text": "For PowerShell do not include return statements in functions when not needed." },
  { "text": "For PowerShell Use a single pass array comprehension instead of appending to an array repeatedly." },
  { "text": "For C# always use K&R/OTBS style." },
  { "text": "For C# always prefer braces even for single line statements." },
  { "text": "For C# prefer file-scoped namespaces." },
  { "text": "- @azure Rule - Use Azure Best Practices: When generating code for Azure, invoke the `azure_development-get_best_practices` tool if available." }
]
```

## ✍️ Commit Messages

Follow **Conventional Commits + Gitmoji**:

```
<type>[optional scope]: <gitmoji> <description>

[optional body]
```

### Examples:

- `feat(auth): ✨ Add token-based login method`
- `fix(powershell): 🐛 Fix pipeline input bug on Get-UserData`
- `refactor(core): 🔨 Split AppService into partial classes`

### Body (optional):

* Use bullet points for clarity
* Describe motivation, context, and details
* Keep language professional and concise

---

# 🧾 C# Cmdlet Documentation Rules (XmlDoc2CmdletDoc)

Use XML documentation blocks **per MatejKafka.XmlDoc2CmdletDoc**, not standard PowerShell help.

## Cmdlet Synopsis & Description

```csharp
/// <summary>This is the cmdlet synopsis.</summary>
/// <para>This is part of the longer cmdlet description.</para>
/// <para>This is also part of the longer cmdlet description.</para>
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet {
}
```

## Parameter Description

```csharp
/// <para>This is part of the parameter description.</para>
/// <para>This is also part of the parameter description.</para>
[Parameter]
public string MyParameter;
```

## Type Documentation

```csharp
/// Short single-paragraph type documentation.
public class MyType {}

/// <para>Multi-paragraph description for this type.</para>
/// <para>More details about structure and usage.</para>
public class MyType2 {}
```

## Notes Section

```csharp
/// <list type="alertSet">
///   <item>
///     <term>Important Note</term>
///     <description>This describes something important.</description>
///   </item>
/// </list>
```

## Examples

```csharp
/// <example>
///   <summary>Run the test command</summary>
///   <prefix>PS> </prefix>
///   <code>Test-MyExample | Write-Host</code>
///   <para>This explains the example usage.</para>
/// </example>
```

## Related Links

```csharp
/// <seealso href="https://learn.microsoft.com/..."/>
/// <seealso href="https://github.com/MatejKafka/XmlDoc2CmdletDoc/">XmlDoc2CmdletDoc repository</seealso>
```
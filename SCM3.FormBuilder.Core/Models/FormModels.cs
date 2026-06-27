using System;
using System.Collections.Generic;

namespace SCM3.FormBuilder.Core.Models;

public class FormDefinition
{
    public Guid FormId { get; set; }
    public string ToolType { get; set; } = "";
    public string FormKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime CreatedDate { get; set; }
}

public class FormVersion
{
    public Guid FormVersionId { get; set; }
    public Guid FormId { get; set; }
    public string ToolType { get; set; } = "";
    public string FormKey { get; set; } = "";
    public int VersionNumber { get; set; }
    public bool IsPublished { get; set; }
    public DateTime PublishedDate { get; set; }
    public List<Section> Sections { get; set; } = new();
}

public class Section
{
    public string SectionId { get; set; } = "";
    public string Title { get; set; } = "";
    public int Order { get; set; }
    public List<Field> Fields { get; set; } = new();
}

public enum FieldType
{
    Text,
    Number,
    Date,
    Dropdown,
    ComboBox,
    Checkbox,
    TextArea,
    MultiSelect,
    DateTimePicker,
    RichText
}

public class Field
{
    public string FieldId { get; set; } = "";
    public FieldType Type { get; set; }
    public string Label { get; set; } = "";

    // 12-column grid placement, per section
    public int GridRow { get; set; }
    public int GridColumn { get; set; } = 1;
    public int GridSpan { get; set; } = 12;

    public List<FieldOption>? Options { get; set; }
    public bool AllowCustomValue { get; set; } // only meaningful when Type == ComboBox
    public ValidationRule? Validation { get; set; }
    public List<ConditionalRule>? ConditionalRules { get; set; }
}

public class FieldOption
{
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
}

public class ValidationRule
{
    public bool Required { get; set; }
    public int? MaxLength { get; set; }
    public string? RegexPattern { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}

public enum ConditionalEffect
{
    Show,
    Hide,
    Enable,
    Disable,
    Lock
}

public class ConditionalRule
{
    public string SourceFieldId { get; set; } = "";
    public string Operator { get; set; } = "equals"; // equals, notEquals, contains, etc.
    public string Value { get; set; } = "";
    public ConditionalEffect Effect { get; set; }
}

public class FormSubmission
{
    public Guid SubmissionId { get; set; }
    public Guid FormVersionId { get; set; }
    public int FormVersionNumber { get; set; } // stored alongside the GUID so the pinned version can be resolved directly, no lookup needed
    public string ToolType { get; set; } = "";
    public string FormKey { get; set; } = "";
    public string RecordPrimaryKey { get; set; } = "";
    public Dictionary<string, SubmissionFieldData> Values { get; set; } = new();
    public DateTime SubmittedDate { get; set; }
    public string SubmittedBy { get; set; } = "";
}

public class SubmissionFieldData
{
    public string Label { get; set; } = "";
    public object? Value { get; set; }
}

public class Artifact
{
    public string ArtifactId { get; set; } = "";  // e.g., "CSCI-2024-001"
    public string Name { get; set; } = "";        // e.g., "Core System CI"
    public string Type { get; set; } = "CSCI";    // CSCI, Segment, or System
    public DateTime CreatedDate { get; set; }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SCM3.FormBuilder.Core.Models;

namespace SCM3.FormBuilder.Core.Evaluation;

public record FieldRenderState(bool Visible, bool Enabled, bool ReadOnly);

public class ConditionalRuleEvaluator
{
    // Given a form version and the current in-progress values, compute
    // the render state for every field. Call this once up front, and again
    // any time a field's value changes.
    public Dictionary<string, FieldRenderState> Evaluate(
        FormVersion version,
        Dictionary<string, object?> currentValues)
    {
        var result = new Dictionary<string, FieldRenderState>();

        var allFields = version.Sections.SelectMany(s => s.Fields);

        foreach (var field in allFields)
        {
            var state = EvaluateField(field, currentValues);
            result[field.FieldId] = state;
        }

        return result;
    }

    private FieldRenderState EvaluateField(Field field, Dictionary<string, object?> currentValues)
    {
        // Defaults: visible, enabled, editable
        bool visible = true;
        bool enabled = true;
        bool readOnly = false;

        if (field.ConditionalRules is not null)
        {
            foreach (var rule in field.ConditionalRules)
            {
                bool conditionTrue = EvaluateCondition(rule, currentValues);
                if (!conditionTrue) continue;

                switch (rule.Effect)
                {
                    case ConditionalEffect.Show:
                        visible = true;
                        break;
                    case ConditionalEffect.Hide:
                        visible = false;
                        break;
                    case ConditionalEffect.Enable:
                        enabled = true;
                        break;
                    case ConditionalEffect.Disable:
                        enabled = false;
                        break;
                    case ConditionalEffect.Lock:
                        readOnly = true;
                        break;
                }
            }
        }

        // Hidden always wins: a hidden field's enabled/readonly state
        // doesn't matter since it isn't rendered at all.
        if (!visible)
        {
            return new FieldRenderState(Visible: false, Enabled: false, ReadOnly: false);
        }

        return new FieldRenderState(visible, enabled, readOnly);
    }

    private bool EvaluateCondition(ConditionalRule rule, Dictionary<string, object?> currentValues)
    {
        if (!currentValues.TryGetValue(rule.SourceFieldId, out var sourceValue))
            return false;

        string? sourceStr = sourceValue?.ToString();

        return rule.Operator switch
        {
            "equals" => string.Equals(sourceStr, rule.Value, StringComparison.OrdinalIgnoreCase),
            "notEquals" => !string.Equals(sourceStr, rule.Value, StringComparison.OrdinalIgnoreCase),
            "contains" => sourceStr?.Contains(rule.Value, StringComparison.OrdinalIgnoreCase) ?? false,
            _ => false
        };
    }
}

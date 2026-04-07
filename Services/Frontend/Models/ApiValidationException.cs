using Microsoft.AspNetCore.Components.Forms;

namespace Frontend.Models;

/// <summary>
/// バリデーションエラー例外（EditForm 連携用）
/// §16.1 準拠
/// </summary>
public class ApiValidationException : Exception
{
    public Dictionary<string, string[]> FieldErrors { get; }

    public ApiValidationException(Dictionary<string, string[]> fieldErrors)
        : base("Validation failed")
    {
        FieldErrors = fieldErrors;
    }

    /// <summary>
    /// EditContext の ValidationMessageStore にエラーを追加
    /// </summary>
    public void ApplyToEditContext(EditContext editContext, ValidationMessageStore messageStore)
    {
        ArgumentNullException.ThrowIfNull(editContext);
        ArgumentNullException.ThrowIfNull(messageStore);

        foreach (var (field, messages) in FieldErrors)
        {
            var fieldName = char.ToLowerInvariant(field[0]) + field[1..];
            var fieldIdentifier = editContext.Field(fieldName);
            foreach (var message in messages)
            {
                messageStore.Add(fieldIdentifier, message);
            }
        }
        editContext.NotifyValidationStateChanged();
    }
}

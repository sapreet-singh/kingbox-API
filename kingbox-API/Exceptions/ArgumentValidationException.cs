namespace KingBox.Api.Exceptions;

/// <summary>
/// Domain validation exception containing the offending field name and validation message.
/// </summary>
public class ArgumentValidationException : Exception
{
    public string FieldName { get; }

    public ArgumentValidationException(string fieldName, string message) : base(message)
    {
        FieldName = fieldName;
    }

    public static ArgumentValidationException ForField(string fieldName, string message) => new(fieldName, message);
}

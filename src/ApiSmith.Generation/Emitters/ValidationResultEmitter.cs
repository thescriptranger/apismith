using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;

namespace ApiSmith.Generation.Emitters;

public static class ValidationResultEmitter
{
    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        string content;
        if (config.ApiVersion == ApiVersion.V2)
        {
            content = $$"""
                using {{layout.SharedErrorsNamespace(config)}};

                namespace {{layout.ValidatorCoreNamespace(config)}};

                public sealed class ValidationResult
                {
                    private readonly System.Collections.Generic.List<ValidationError> _errors = new();

                    public System.Collections.Generic.IReadOnlyList<ValidationError> Errors => _errors;

                    public bool IsValid => _errors.Count == 0;

                    public void Add(string propertyName, string message) =>
                        _errors.Add(new ValidationError(propertyName, message));
                }

                public interface IValidator<in TDto>
                {
                    ValidationResult Validate(TDto dto);
                }
                """;
        }
        else
        {
            content = $$"""
                namespace {{layout.ValidatorCoreNamespace(config)}};

                public sealed record ValidationError(string PropertyName, string Message);

                public sealed class ValidationResult
                {
                    private readonly System.Collections.Generic.List<ValidationError> _errors = new();

                    public System.Collections.Generic.IReadOnlyList<ValidationError> Errors => _errors;

                    public bool IsValid => _errors.Count == 0;

                    public void Add(string propertyName, string message) =>
                        _errors.Add(new ValidationError(propertyName, message));
                }

                public interface IValidator<in TDto>
                {
                    ValidationResult Validate(TDto dto);
                }
                """;
        }

        return new EmittedFile(layout.ValidationCorePath(config), content);
    }
}

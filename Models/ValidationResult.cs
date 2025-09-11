using System.Collections.Generic;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Represents the result of a validation operation.
    /// Contains information about whether validation passed and any errors or warnings.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Indicates whether the validation was successful.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// List of validation errors that prevent the operation from succeeding.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings that don't prevent the operation but indicate potential issues.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets a value indicating whether there are any validation messages (errors or warnings).
        /// </summary>
        public bool HasMessages => Errors.Count > 0 || Warnings.Count > 0;

        /// <summary>
        /// Gets the total count of validation messages.
        /// </summary>
        public int MessageCount => Errors.Count + Warnings.Count;

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <returns>Validation result indicating success</returns>
        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// Creates a failed validation result with specified errors.
        /// </summary>
        /// <param name="errors">Validation errors</param>
        /// <returns>Validation result indicating failure</returns>
        public static ValidationResult Failure(params string[] errors)
        {
            var result = new ValidationResult { IsValid = false };
            result.Errors.AddRange(errors);
            return result;
        }

        /// <summary>
        /// Creates a successful validation result with warnings.
        /// </summary>
        /// <param name="warnings">Validation warnings</param>
        /// <returns>Validation result with warnings</returns>
        public static ValidationResult WithWarnings(params string[] warnings)
        {
            var result = new ValidationResult { IsValid = true };
            result.Warnings.AddRange(warnings);
            return result;
        }

        /// <summary>
        /// Adds an error to the validation result and marks it as invalid.
        /// </summary>
        /// <param name="error">Error message to add</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Adds a warning to the validation result.
        /// </summary>
        /// <param name="warning">Warning message to add</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Combines this validation result with another.
        /// </summary>
        /// <param name="other">Other validation result to combine</param>
        /// <returns>Combined validation result</returns>
        public ValidationResult Combine(ValidationResult other)
        {
            var combined = new ValidationResult
            {
                IsValid = IsValid && other.IsValid
            };

            combined.Errors.AddRange(Errors);
            combined.Errors.AddRange(other.Errors);
            combined.Warnings.AddRange(Warnings);
            combined.Warnings.AddRange(other.Warnings);

            return combined;
        }

        /// <summary>
        /// Returns a string representation of the validation result.
        /// </summary>
        /// <returns>String describing the validation result</returns>
        public override string ToString()
        {
            if (IsValid && !HasMessages)
                return "Validation successful";

            var parts = new List<string>();
            
            if (!IsValid)
                parts.Add($"FAILED with {Errors.Count} error(s)");
            else
                parts.Add("PASSED");

            if (Warnings.Count > 0)
                parts.Add($"{Warnings.Count} warning(s)");

            return string.Join(", ", parts);
        }
    }
} 
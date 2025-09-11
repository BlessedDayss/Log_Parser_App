using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Represents a complete filter configuration that can be saved and loaded.
    /// Supports both simple criteria-based filters and complex composite expressions.
    /// </summary>
    public class FilterConfiguration
    {
        /// <summary>
        /// Unique name for this configuration.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of what this filter does.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Configuration schema version for compatibility.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Timestamp when this configuration was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Timestamp when this configuration was last modified.
        /// </summary>
        [JsonPropertyName("lastModified")]
        public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Simple filter criteria (for basic filtering).
        /// </summary>
        [JsonPropertyName("criteria")]
        public List<FilterCriterion> Criteria { get; set; } = new();

        /// <summary>
        /// Complex filter expression (for advanced filtering).
        /// Stored as JSON structure that can be rebuilt into IFilterExpression.
        /// </summary>
        [JsonPropertyName("complexExpression")]
        public FilterExpressionData? ComplexExpression { get; set; }

        /// <summary>
        /// Configuration type indicating the complexity level.
        /// </summary>
        [JsonPropertyName("type")]
        public FilterConfigurationType Type { get; set; } = FilterConfigurationType.Simple;

        /// <summary>
        /// Tags for organizing and searching configurations.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Indicates if this configuration is a system default.
        /// </summary>
        [JsonPropertyName("isSystem")]
        public bool IsSystem { get; set; } = false;

        /// <summary>
        /// User who created this configuration.
        /// </summary>
        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets the effective filter criteria.
        /// Returns simple criteria if available, otherwise converts complex expression.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<FilterCriterion> EffectiveCriteria
        {
            get
            {
                if (Type == FilterConfigurationType.Simple && Criteria.Any())
                    return Criteria;

                if (Type == FilterConfigurationType.Complex && ComplexExpression != null)
                    return ExtractCriteriaFromExpression(ComplexExpression);

                return Enumerable.Empty<FilterCriterion>();
            }
        }

        /// <summary>
        /// Creates a simple filter configuration from criteria.
        /// </summary>
        /// <param name="name">Configuration name</param>
        /// <param name="criteria">Filter criteria</param>
        /// <param name="description">Optional description</param>
        /// <returns>Simple filter configuration</returns>
        public static FilterConfiguration CreateSimple(string name, IEnumerable<FilterCriterion> criteria, string? description = null)
        {
            return new FilterConfiguration
            {
                Name = name,
                Description = description,
                Type = FilterConfigurationType.Simple,
                Criteria = criteria.ToList()
            };
        }

        /// <summary>
        /// Creates a complex filter configuration from expression data.
        /// </summary>
        /// <param name="name">Configuration name</param>
        /// <param name="expression">Complex filter expression</param>
        /// <param name="description">Optional description</param>
        /// <returns>Complex filter configuration</returns>
        public static FilterConfiguration CreateComplex(string name, FilterExpressionData expression, string? description = null)
        {
            return new FilterConfiguration
            {
                Name = name,
                Description = description,
                Type = FilterConfigurationType.Complex,
                ComplexExpression = expression
            };
        }

        /// <summary>
        /// Validates the configuration for consistency and completeness.
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult { IsValid = true };

            // Validate name
            if (string.IsNullOrWhiteSpace(Name))
            {
                result.Errors.Add("Configuration name cannot be empty");
                result.IsValid = false;
            }

            // Validate schema version
            if (string.IsNullOrWhiteSpace(SchemaVersion))
            {
                result.Errors.Add("Schema version cannot be empty");
                result.IsValid = false;
            }

            // Validate based on type
            switch (Type)
            {
                case FilterConfigurationType.Simple:
                    if (!Criteria.Any())
                    {
                        result.Errors.Add("Simple configuration must have at least one criterion");
                        result.IsValid = false;
                    }
                    break;

                case FilterConfigurationType.Complex:
                    if (ComplexExpression == null)
                    {
                        result.Errors.Add("Complex configuration must have expression data");
                        result.IsValid = false;
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// Extracts filter criteria from complex expression data.
        /// Used for compatibility and simple UI representation.
        /// </summary>
        /// <param name="expression">Expression data to extract from</param>
        /// <returns>Extracted criteria</returns>
        private IEnumerable<FilterCriterion> ExtractCriteriaFromExpression(FilterExpressionData expression)
        {
            var criteria = new List<FilterCriterion>();

            if (expression.Type == FilterExpressionType.Leaf && expression.Criterion != null)
            {
                criteria.Add(expression.Criterion);
            }
            else if (expression.Type == FilterExpressionType.Composite && expression.Children != null)
            {
                foreach (var child in expression.Children)
                {
                    criteria.AddRange(ExtractCriteriaFromExpression(child));
                }
            }

            return criteria;
        }
    }

    /// <summary>
    /// Types of filter configurations supported.
    /// </summary>
    public enum FilterConfigurationType
    {
        /// <summary>
        /// Simple configuration using basic criteria list.
        /// </summary>
        Simple,

        /// <summary>
        /// Complex configuration using nested expressions.
        /// </summary>
        Complex
    }

    /// <summary>
    /// Serializable representation of filter expression structure.
    /// Used to persist complex filter expressions in JSON format.
    /// </summary>
    public class FilterExpressionData
    {
        /// <summary>
        /// Type of expression (Leaf or Composite).
        /// </summary>
        [JsonPropertyName("type")]
        public FilterExpressionType Type { get; set; }

        /// <summary>
        /// Logical operator for composite expressions.
        /// </summary>
        [JsonPropertyName("operator")]
        public LogicalOperator? Operator { get; set; }

        /// <summary>
        /// Filter criterion for leaf expressions.
        /// </summary>
        [JsonPropertyName("criterion")]
        public FilterCriterion? Criterion { get; set; }

        /// <summary>
        /// Child expressions for composite expressions.
        /// </summary>
        [JsonPropertyName("children")]
        public List<FilterExpressionData>? Children { get; set; }

        /// <summary>
        /// Optional description for this expression node.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Types of filter expressions in the serializable format.
    /// </summary>
    public enum FilterExpressionType
    {
        /// <summary>
        /// Leaf expression containing a single criterion.
        /// </summary>
        Leaf,

        /// <summary>
        /// Composite expression containing child expressions.
        /// </summary>
        Composite
    }
} 
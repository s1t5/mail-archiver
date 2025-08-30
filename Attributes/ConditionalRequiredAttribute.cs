using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Attributes
{
    public class ConditionalRequiredAttribute : RequiredAttribute
    {
        private readonly string _dependentProperty;
        private readonly object _targetValue;

        public ConditionalRequiredAttribute(string dependentProperty, object targetValue)
        {
            _dependentProperty = dependentProperty;
            _targetValue = targetValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var property = validationContext.ObjectType.GetProperty(_dependentProperty);
            if (property == null)
            {
                return new ValidationResult($"Unknown property: {_dependentProperty}");
            }

            var dependentValue = property.GetValue(validationContext.ObjectInstance);
            
            // If the dependent property equals the target value, apply required validation
            if (Equals(dependentValue, _targetValue))
            {
                return base.IsValid(value, validationContext);
            }

            // Otherwise, validation passes
            return ValidationResult.Success;
        }
    }
}

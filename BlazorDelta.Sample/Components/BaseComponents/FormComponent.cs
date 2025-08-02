using BlazorDelta.Abstractions;
using BlazorDelta.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Linq.Expressions;

namespace BlazorDelta.Sample.Components.BaseComponents
{
    public partial class FormComponent<T> : BaseUIComponent, IDisposable
    {
        [CascadingParameter]
        public EditContext? EditContext { get; set; }

        [Parameter]
        public T? Value { get; set; }

        [Parameter]
        public EventCallback<T?> ValueChanged { get; set; }

        [Parameter]
        public Expression<Func<T>>? ValueExpression { get; set; }

        [Parameter, UpdatesCss]
        public bool Disabled { get; set; }

        [Parameter, UpdatesCss]
        public bool ReadOnly { get; set; }


        protected FieldIdentifier? _fielIdentifier;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            HandleEditContextChanged();
        }

        [OnParameterChanged(nameof(EditContext))]
        public void HandleEditContextChanged()
        {
            if (EditContext == null || ValueExpression == null)
            {
                return;
            }

            _fielIdentifier = FieldIdentifier.Create(ValueExpression);
            
            EditContext.OnFieldChanged -= OnFieldChanged;
            EditContext.OnFieldChanged += OnFieldChanged;
            EditContext.OnValidationStateChanged -= OnValidationChange;
            EditContext.OnValidationStateChanged += OnValidationChange;
        }


        public void OnFieldChanged(object? sender, FieldChangedEventArgs args)
        {
            if (EditContext == null || _fielIdentifier == null)
            {
                if (_editContextCss != null)
                {
                    _editContextCss = null;
                    DirtyCss = true;
                }
                return;
            }

            if (args.FieldIdentifier.Equals(_fielIdentifier.Value))
            {
                _editContextCss = EditContext.FieldCssClass(_fielIdentifier.Value);
                DirtyCss = true;
            }
            //handle field change or smthing bruh.
        }


        public void OnValidationChange(object? sender, ValidationStateChangedEventArgs args)
        {

        }


        protected string? _cssClass = string.Empty;

        private string? _editContextCss = null;

        protected override void UpdateCssClasses()
        {
            _cssClass = $"form-control";

            if (Disabled)
            {
                _cssClass += " disabled";
            }
            if (ReadOnly)
            {
                _cssClass += " readonly";
            }
            if (_editContextCss != null)
            {
                _cssClass = $" {_editContextCss}";
            }

            base.UpdateCssClasses();
        }

        public void Dispose()
        {
            if (EditContext != null)
            {
                EditContext.OnFieldChanged -= OnFieldChanged;
                EditContext.OnValidationStateChanged -= OnValidationChange;
            }
        }
    }
}

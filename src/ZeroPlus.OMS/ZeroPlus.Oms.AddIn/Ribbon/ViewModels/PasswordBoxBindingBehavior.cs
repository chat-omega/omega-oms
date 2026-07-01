using Microsoft.Xaml.Behaviors;
using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ZeroPlus.Oms.AddIn.Ribbon.ViewModels
{
    public class PasswordBoxBindingBehavior : Behavior<PasswordBox>
    {
        public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(nameof(Password), typeof(SecureString), typeof(PasswordBoxBindingBehavior), new PropertyMetadata(null));

        public SecureString Password
        {
            get => (SecureString)GetValue(PasswordProperty);
            set => SetValue(PasswordProperty, value);
        }

        protected override void OnAttached()
        {
            AssociatedObject.PasswordChanged += new RoutedEventHandler(OnPasswordBoxValueChanged);
        }

        private void OnPasswordBoxValueChanged(object d, RoutedEventArgs e)
        {
            BindingExpression bindingExpression = BindingOperations.GetBindingExpression(this, PasswordProperty);
            if (bindingExpression == null)
            {
                return;
            }

            PropertyInfo property = bindingExpression.DataItem.GetType().GetProperty(bindingExpression.ParentBinding.Path.Path);
            if (!(property != null))
            {
                return;
            }

            property.SetValue(bindingExpression.DataItem, AssociatedObject.SecurePassword, null);
        }
    }
}

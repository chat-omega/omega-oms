using System;
using System.Windows.Markup;

namespace ZeroPlus.Oms.AddIn.Ribbon.Helpers
{
    public class DISource : MarkupExtension
    {
        public static Func<Type, object> Resolver { get; set; }
        public Type Type { get; set; }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Resolver?.Invoke(Type);
        }
    }
}

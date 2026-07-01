using System;

namespace ZeroPlus.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class GridVisibleByDefaultAttribute : Attribute
    {
        public bool IsVisible { get; }
        public GridVisibleByDefaultAttribute(bool isVisible = true)
        {
            IsVisible = isVisible;
        }
    }
}

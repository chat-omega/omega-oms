using System.Collections.Generic;
using ZeroPlus.Oms.Data;

namespace ZeroPlus.Oms.Ui.Models
{
    public class PermOperationModel
    {
        public string Title { get; set; }
        public List<PermOperationMode> Perms { get; set; }

        public PermOperationModel() { }

        public PermOperationModel(string title, List<PermOperationMode> perms)
        {
            Title = title;
            Perms = perms;
        }

        public override bool Equals(object obj)
        {
            if (obj is not PermOperationModel other)
            {
                return false;
            }
            return Title == other.Title && Perms.Count == other.Perms.Count;
        }

        public override int GetHashCode()
        {
            return Title.GetHashCode();
        }
    }
}

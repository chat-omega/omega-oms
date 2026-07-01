using System;
using System.Linq;

namespace ZeroPlus.Oms.Ui.Models
{
    public enum ChangeTypes
    {
        NEW,
        ENH,
        BUG,
    }

    public class ReleaseNoteModel
    {
        public ChangeTypes ChangeType { get; set; } = ChangeTypes.NEW;
        public string Note { get; set; } = "";

        internal void Update(string releaseNote)
        {
            if (releaseNote != null)
            {
                string[] noteParts = releaseNote.Split(' ');
                if (noteParts.Length > 1)
                {
                    if (Enum.TryParse(noteParts[0], true, out ChangeTypes changeType))
                    {
                        ChangeType = changeType;
                        Note = string.Join(" ", noteParts.Skip(1));
                        if (Note.StartsWith("->"))
                        {
                            Note = Note.Replace("->", "    ➤");
                        }
                        return;
                    }
                }
                Note = releaseNote;
            }
        }
    }
}
namespace BuddyAI.Models
{
    public class PatchInstruction
    {
        public int Line { get; set; }
        public string Replacement { get; set; } = "";
    }
}
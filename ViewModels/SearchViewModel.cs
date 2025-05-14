using Microsoft.AspNetCore.Mvc.Rendering;

namespace MailArchiver.Models.ViewModels
{
    public class SearchViewModel
    {
        public string SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SelectedAccountId { get; set; }
        public bool? IsOutgoing { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        // Dropdown-Optionen
        public List<SelectListItem> AccountOptions { get; set; }
        public List<SelectListItem> DirectionOptions { get; set; } = new List<SelectListItem>
        {
            new SelectListItem { Text = "All", Value = "" },
            new SelectListItem { Text = "Incoming", Value = "false" },
            new SelectListItem { Text = "Outgoing", Value = "true" }
        };

        // Suchergebnisse
        public List<ArchivedEmail> SearchResults { get; set; }
        public int TotalResults { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalResults / (double)PageSize);

        // Für die Batch-Operationen
        public List<int> SelectedEmailIds { get; set; } = new List<int>();
        public bool ShowSelectionControls { get; set; } = false;
    }
}
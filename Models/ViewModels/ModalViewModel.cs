using System.Collections.Generic;

namespace MailArchiver.Models.ViewModels
{
    public class ModalViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Size { get; set; } = "modal-md"; // modal-sm, modal-lg, modal-xl
        
        // List section
        public bool IncludeList { get; set; } = false;
        public string ListTitle1 { get; set; }
        public List<string> ListItems1 { get; set; } = new List<string>();
        public string ListTitle2 { get; set; }
        public List<string> ListItems2 { get; set; } = new List<string>();
        
        // Warning section
        public bool ShowWarning { get; set; } = false;
        public string WarningMessage { get; set; }
        
        // Buttons
        public bool ShowCancelButton { get; set; } = true;
        public string CancelButtonText { get; set; } = "Cancel";
        public bool ShowConfirmButton { get; set; } = true;
        public string ConfirmButtonText { get; set; } = "Confirm";
        public string ConfirmButtonClass { get; set; } = "btn-primary";
    }
}

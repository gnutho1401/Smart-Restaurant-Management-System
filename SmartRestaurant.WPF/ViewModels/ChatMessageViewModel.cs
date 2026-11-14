using System;

namespace SmartRestaurant.WPF.ViewModels;

public class ChatMessageViewModel
{
    public string Sender { get; set; } = "Assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsUser => Sender.Equals("User", StringComparison.OrdinalIgnoreCase);
}

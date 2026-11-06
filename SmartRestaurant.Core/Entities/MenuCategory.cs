using System.Collections.Generic;

namespace SmartRestaurant.Core.Entities;

/// <summary>
/// Menu classification entity (e.g. Appetizers, Main Courses, Desserts, Beverages).
/// </summary>
public class MenuCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}

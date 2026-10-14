using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Client.Models
{
    public class Position
    {
        public int PositionID { get; set; }

        [Required]
        [StringLength(50)]
        public string PositionName { get; set; } = string.Empty;
    }
}
using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Client.Models
{
    public class Employee
    {
        public int EmployeeID { get; set; }

        [Required(ErrorMessage = "Meno je povinné.")]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Priezvisko je povinné.")]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Dátum narodenia je povinný.")]
        public DateTime BirthDate { get; set; }

        public int? PositionID { get; set; }
        public Position? Position { get; set; }

        [Required(ErrorMessage = "IP adresa je povinná.")]
        [StringLength(45)]
        public string IPaddress { get; set; } = string.Empty;

        public string? IPCountryCode { get; set; }
    }
}
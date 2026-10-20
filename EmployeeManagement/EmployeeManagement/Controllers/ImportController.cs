using EmployeeManagement.Client.Models;
using EmployeeManagement.Data;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace EmployeeManagement.Controllers
{
    // ---------------------------------------------------------------
    // Triedy, ktoré opisujú, ako vyzerajú importné JSON súbory
    // ---------------------------------------------------------------

    // Súbor s pozíciami: { "positions": [ "Manager", "Developer", ... ] }
    public class PositionsFile
    {
        public List<string> Positions { get; set; } = new();
    }

    // Súbor so zamestnancami: { "employees": [ { ... }, { ... } ] }
    public class EmployeesFile
    {
        public List<EmployeeImportItem> Employees { get; set; } = new();
    }

    // Jeden zamestnanec v súbore. BirthDate je text (napr. "1988/09/05")
    // a Position je názov pozície, nie číslo. Prevedieme ich až pri importe.
    public class EmployeeImportItem
    {
        public string Name { get; set; } = "";
        public string Surname { get; set; } = "";
        public string BirthDate { get; set; } = "";
        public string? Position { get; set; }
        public string IpAddress { get; set; } = "";
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ImportController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IpCountryService _ipService;

        public ImportController(AppDbContext db, IpCountryService ipService)
        {
            _db = db;
            _ipService = ipService;
        }

        // ---------------------------------------------------------------
        // IMPORT POZÍCIÍ  (POST api/import/positions)
        // ---------------------------------------------------------------
        [HttpPost("positions")]
        public async Task<IActionResult> ImportPositions(IFormFile file)
        {
            // Súbor musí byť platný JSON, inak vrátime zrozumiteľnú chybu
            PositionsFile? data;
            try
            {
                data = await JsonSerializer.DeserializeAsync<PositionsFile>(
                    file.OpenReadStream(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Súbor nie je platný JSON alebo nemá správny tvar." });
            }

            int added = 0;
            foreach (var name in data?.Positions ?? new())
            {
                // Odstránime medzery na začiatku a na konci
                var trimmed = name.Trim();

                // KONTROLA: prázdny názov preskočíme
                if (trimmed == "") continue;

                // KONTROLA: pozícia s rovnakým názvom už v databáze je -> preskočíme
                bool exists = await _db.Positions.AnyAsync(p => p.PositionName == trimmed);
                if (exists) continue;

                _db.Positions.Add(new Position { PositionName = trimmed });
                await _db.SaveChangesAsync();
                added++;
            }

            return Ok(new { message = $"Pridaných pozícií: {added}." });
        }

        // ---------------------------------------------------------------
        // IMPORT ZAMESTNANCOV  (POST api/import/employees)
        // ---------------------------------------------------------------
        [HttpPost("employees")]
        public async Task<IActionResult> ImportEmployees(IFormFile file)
        {
            // KONTROLA 1: súbor musí byť platný JSON v správnom tvare.
            // Ak nie je, vrátime zrozumiteľnú chybu namiesto pádu aplikácie.
            EmployeesFile? data;
            try
            {
                data = await JsonSerializer.DeserializeAsync<EmployeesFile>(
                    file.OpenReadStream(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest(new { message = "Súbor nie je platný JSON alebo nemá správny tvar." });
            }

            // KONTROLA 2: súbor nesmie byť prázdny
            if (data == null || data.Employees.Count == 0)
                return BadRequest(new { message = "Súbor neobsahuje žiadnych zamestnancov." });

            int added = 0;     // koľko zamestnancov sme pridali
            int skipped = 0;   // koľko sme preskočili

            // Každého zamestnanca zo súboru pustíme cez niekoľko "brán".
            // Ak ho niektorá zastaví, započítame ho medzi preskočených a ideme na ďalšieho.
            foreach (var item in data.Employees)
            {
                // BRÁNA 0: meno a priezvisko musia byť vyplnené a IP musí mať platný tvar
                if (string.IsNullOrWhiteSpace(item.Name) ||
                    string.IsNullOrWhiteSpace(item.Surname) ||
                    !IsValidIp(item.IpAddress))
                {
                    skipped++;
                    continue;
                }

                // BRÁNA 1: dátum narodenia musí byť vo formáte RRRR/MM/DD (napr. 1988/09/05).
                // Text zo súboru zároveň prevedieme na skutočný dátum (birthDate).
                bool dateOk = DateTime.TryParseExact(
                    item.BirthDate, "yyyy/MM/dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out DateTime birthDate);

                if (!dateOk)
                {
                    skipped++;
                    continue;
                }

                // BRÁNA 2: duplicita. Ak už v databáze je zamestnanec s rovnakým menom,
                // priezviskom a dátumom narodenia, preskočíme ho.
                bool exists = await _db.Employees.AnyAsync(e =>
                    e.Name == item.Name &&
                    e.Surname == item.Surname &&
                    e.BirthDate == birthDate);

                if (exists)
                {
                    skipped++;
                    continue;
                }

                // POZÍCIA: v súbore je názov (napr. "Manager"), v databáze potrebujeme číslo (PositionID).
                // Ak pozíciu nenájdeme, nechávame PositionID prázdne a zamestnanca aj tak uložíme.
                int? positionId = null;
                if (!string.IsNullOrWhiteSpace(item.Position))
                {
                    var position = await _db.Positions
                        .FirstOrDefaultAsync(p => p.PositionName == item.Position.Trim());

                    positionId = position?.PositionID;
                }

                // ULOŽENIE: vytvoríme zamestnanca. Kód krajiny sa zistí z IP adresy cez externé API
                // (ak sa to nepodarí, ostane prázdny, ale zamestnanec sa uloží).
                var employee = new Employee
                {
                    Name = item.Name,
                    Surname = item.Surname,
                    BirthDate = birthDate,
                    PositionID = positionId,
                    IPaddress = item.IpAddress.Trim(),
                    IPCountryCode = await _ipService.GetCountryCodeAsync(item.IpAddress.Trim())
                };

                _db.Employees.Add(employee);
                await _db.SaveChangesAsync();
                added++;
            }

            return Ok(new { message = $"Pridaných: {added}, preskočených: {skipped}." });
        }

        // Kontrola IP adresy: musí byť IPv4 s presne 4 číslami (napr. 8.8.8.8).
        // Odmietne prázdny text, "8", "8.8.8", "abc" aj IPv6 adresy.
        private static bool IsValidIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            ip = ip.Trim();

            return IPAddress.TryParse(ip, out var address)
                && address.AddressFamily == AddressFamily.InterNetwork
                && ip.Split('.').Length == 4;
        }
    }
}
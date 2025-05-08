using Microsoft.AspNetCore.Mvc;

namespace Travel.Controllers;

/// <summary>
/// Kontroler do pracy z wycieczkami.
/// </summary>
[ApiController]
[Route("api/trips")]
public class TripsController : ControllerBase
{
    private readonly DatabaseService db;

    public TripsController(DatabaseService db)
    {
        this.db = db;
    }

    /// <summary>
    /// Pobranie wszystkich dostępnych wycieczek.
    /// </summary>
    /// <returns>Lista wycieczek.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllTrips()
    {
        var trips = await db.GetTripsAsync();
        return Ok(trips);
    }
}

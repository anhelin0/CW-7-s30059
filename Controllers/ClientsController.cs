using Microsoft.AspNetCore.Mvc;
    
namespace Travel.Controllers;

/// <summary>
/// Kontroler do pracy z klientami.
/// </summary>
[ApiController]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly DatabaseService db;

    public ClientsController(DatabaseService db)
    {
        this.db = db;
    }

    /// <summary>
    /// Tworzenie nowego klienta.
    /// </summary>
    /// <param name="dto">Dane klienta do utworzenia.</param>
    /// <returns>Id nowego klienta.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateClient([FromBody] Models.ClientCreateDto dto)
    {
        try
        {
            var id = await db.CreateClientAsync(dto);
            return Created($"api/clients/{id}", new { id });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>
    /// Pobranie wycieczek dla konkretnego klienta.
    /// </summary>
    /// <param name="id">Id klienta.</param>
    /// <returns>Lista wycieczek klienta.</returns>
    [HttpGet("{id}/trips")]
    public async Task<IActionResult> GetTripsForClient(int id)
    {
        try
        {
            var trips = await db.GetClientTripsAsync(id);
            return Ok(trips);
        }
        catch (Exception e)
        {
            return NotFound(e.Message);
        }
    }

    /// <summary>
    /// Rejestracja klienta na wycieczkę.
    /// </summary>
    /// <param name="id">Id klienta.</param>
    /// <param name="tripId">Id wycieczki.</param>
    /// <returns>Status rejestracji klienta.</returns>
    [HttpPut("{id}/trips/{tripId}")]
    public async Task<IActionResult> RegisterClient(int id, int tripId)
    {
        try
        {
            await db.RegisterClientForTripAsync(id, tripId);
            return Ok("Klient zarejestrowany pomyślnie");
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    /// <summary>
    /// Usunięcie rejestracji klienta z wycieczki.
    /// </summary>
    /// <param name="id">Id klienta.</param>
    /// <param name="tripId">Id wycieczki.</param>
    /// <returns>Status anulowania rejestracji.</returns>
    [HttpDelete("{id}/trips/{tripId}")]
    public async Task<IActionResult> UnregisterClient(int id, int tripId)
    {
        try
        {
            await db.UnregisterClientFromTripAsync(id, tripId);
            return Ok("Klient wypisany pomyślnie");
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
}

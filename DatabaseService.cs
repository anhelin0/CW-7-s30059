using Microsoft.Data.SqlClient;
using Travel.Models;
using System.Text.RegularExpressions;

namespace Travel;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<List<TripDto>> GetTripsAsync()
    {
        var trips = new List<TripDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(@"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                   c.IdCountry, c.Name AS CountryName
            FROM Trip t
            LEFT JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
            LEFT JOIN Country c ON ct.IdCountry = c.IdCountry
            ORDER BY t.IdTrip", connection);

        using var reader = await cmd.ExecuteReaderAsync();

        TripDto? currentTrip = null;
        while (await reader.ReadAsync())
        {
            var idTrip = reader.GetInt32(0);
            if (currentTrip == null || currentTrip.IdTrip != idTrip)
            {
                currentTrip = new TripDto
                {
                    IdTrip = idTrip,
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DateFrom = reader.GetDateTime(3),
                    DateTo = reader.GetDateTime(4),
                    MaxPeople = reader.GetInt32(5),
                    Countries = new List<string>()
                };
                trips.Add(currentTrip);
            }

            if (!reader.IsDBNull(6))
                currentTrip.Countries.Add(reader.GetString(7));
        }

        return trips;
    }

    public async Task<List<ClientTripDto>> GetClientTripsAsync(int clientId)
    {
        var trips = new List<ClientTripDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkCmd = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @IdClient", connection);
        checkCmd.Parameters.AddWithValue("@IdClient", clientId);

        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists == null)
            throw new Exception("Client not found");

        var cmd = new SqlCommand(@"
            SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                   ct.RegisteredAt, ct.PaymentDate
            FROM Client_Trip ct
            JOIN Trip t ON ct.IdTrip = t.IdTrip
            WHERE ct.IdClient = @IdClient", connection);

        cmd.Parameters.AddWithValue("@IdClient", clientId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            trips.Add(new ClientTripDto
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                RegisteredAt = ParseDateInt(reader.GetInt32(6)),
                PaymentDate = reader.IsDBNull(7) ? null : ParseDateInt(reader.GetInt32(7))
            });
        }

        return trips;
    }

    public async Task<int> CreateClientAsync(ClientCreateDto dto)
    {
        if (!Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new Exception("Invalid email format");
        }

        if (dto.Pesel != null && !Regex.IsMatch(dto.Pesel, @"^\d{11}$"))
        {
            throw new Exception("Invalid Pesel format");
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new SqlCommand(@"
            INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
            OUTPUT INSERTED.IdClient
            VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)", connection);

        cmd.Parameters.AddWithValue("@FirstName", dto.FirstName);
        cmd.Parameters.AddWithValue("@LastName", dto.LastName);
        cmd.Parameters.AddWithValue("@Email", dto.Email);
        cmd.Parameters.AddWithValue("@Telephone", (object?)dto.Telephone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Pesel", (object?)dto.Pesel ?? DBNull.Value);

        return (int)await cmd.ExecuteScalarAsync();
    }

    public async Task RegisterClientForTripAsync(int clientId, int tripId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkClient = new SqlCommand("SELECT 1 FROM Client WHERE IdClient = @clientId", connection);
        checkClient.Parameters.AddWithValue("@clientId", clientId);
        if (await checkClient.ExecuteScalarAsync() is null)
            throw new Exception("Client not found");

        var checkTrip = new SqlCommand("SELECT MaxPeople FROM Trip WHERE IdTrip = @tripId", connection);
        checkTrip.Parameters.AddWithValue("@tripId", tripId);
        var maxPeople = await checkTrip.ExecuteScalarAsync();
        if (maxPeople is null)
            throw new Exception("Trip not found");

        var checkExisting = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId", connection);
        checkExisting.Parameters.AddWithValue("@clientId", clientId);
        checkExisting.Parameters.AddWithValue("@tripId", tripId);
        if (await checkExisting.ExecuteScalarAsync() is not null)
            throw new Exception("Client is already registered for this trip");

        var countCmd = new SqlCommand("SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @tripId", connection);
        countCmd.Parameters.AddWithValue("@tripId", tripId);
        var registered = (int)await countCmd.ExecuteScalarAsync();
        if (registered >= (int)maxPeople)
            throw new Exception("Trip has reached maximum capacity");

        var today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
        var insertCmd = new SqlCommand(@"
            INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt)
            VALUES (@clientId, @tripId, @date)", connection);
        insertCmd.Parameters.AddWithValue("@clientId", clientId);
        insertCmd.Parameters.AddWithValue("@tripId", tripId);
        insertCmd.Parameters.AddWithValue("@date", today);
        await insertCmd.ExecuteNonQueryAsync();
    }

    public async Task UnregisterClientFromTripAsync(int clientId, int tripId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkCmd = new SqlCommand("SELECT 1 FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId", connection);
        checkCmd.Parameters.AddWithValue("@clientId", clientId);
        checkCmd.Parameters.AddWithValue("@tripId", tripId);
        if (await checkCmd.ExecuteScalarAsync() is null)
            throw new Exception("Registration not found");

        var deleteCmd = new SqlCommand("DELETE FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId", connection);
        deleteCmd.Parameters.AddWithValue("@clientId", clientId);
        deleteCmd.Parameters.AddWithValue("@tripId", tripId);
        await deleteCmd.ExecuteNonQueryAsync();
    }

    private DateTime ParseDateInt(int yyyymmdd)
    {
        var y = yyyymmdd / 10000;
        var m = (yyyymmdd % 10000) / 100;
        var d = yyyymmdd % 100;
        return new DateTime(y, m, d);
    }
}

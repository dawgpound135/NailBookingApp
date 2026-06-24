using System.ComponentModel.DataAnnotations;

namespace NailBookingApp.Models;

public class BookingRequest
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string PhoneNumber { get; set; } = "";

    [Required]
    public string Service { get; set; } = "";

    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    public string AppointmentTime { get; set; } = "";

    public string? Notes { get; set; }
}
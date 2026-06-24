using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NailBookingApp.Models;
using NailBookingApp.Services;
using System.Linq;

namespace NailBookingApp.Pages;

public class BookModel : PageModel
{
    private readonly GoogleCalendarService _calendarService;
    public List<string> AvailableTimeSlots { get; set; } = new();
    public DateTime MinimumBookingDate => DateTime.Today.AddDays(7);

    public BookModel(GoogleCalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [BindProperty]
    public BookingRequest Booking { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        Booking.AppointmentDate = MinimumBookingDate;
    }

    public void CellNumberInputHandler()
    {
        var phoneNumber = Request.Form["Booking.PhoneNumber"].ToString();
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            // Strip non-digits, then cap at 11 characters
            var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
            Booking.PhoneNumber = digitsOnly.Length > 10 ? digitsOnly[..10] : digitsOnly;
        }
    }

    public void NameInputHandler()
    {
        var name = Request.Form["Booking.Name"].ToString();
        if (!string.IsNullOrEmpty(name))
        {
            // Remove digits and special characters, allow only letters and spaces
            var cleanedName = new string(name.Where(c => char.IsLetter(c) || char.IsWhiteSpace(c)).ToArray());
            Booking.Name = cleanedName;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
    if (Booking.AppointmentDate.Date < MinimumBookingDate)
    {
        ErrorMessage = $"Appointments must be booked at least one week in advance. Please choose {MinimumBookingDate:dddd, dd MMMM yyyy} or later.";
        return Page();
    }

    if (Request.Form.ContainsKey("checkAvailability"))
    {
        if (string.IsNullOrWhiteSpace(Booking.Service))
        {
            ErrorMessage = "Please select a service first.";
            return Page();
        }

        AvailableTimeSlots = await _calendarService.GetAvailableTimeSlotsAsync(
            Booking.AppointmentDate,
            Booking.Service
        );

        return Page();
    }

    if (!ModelState.IsValid)
    {
        ErrorMessage = "Please fill in all required fields.";
        return Page();
    }

    try
    {
        var isAvailable = await _calendarService.IsSlotAvailableAsync(Booking);

        if (!isAvailable)
        {
            ErrorMessage = "Sorry, that time has just been booked. Please choose another time.";
            AvailableTimeSlots = await _calendarService.GetAvailableTimeSlotsAsync(
                Booking.AppointmentDate,
                Booking.Service
            );
            return Page();
        }

        await _calendarService.CreateBookingEventAsync(Booking);
        return RedirectToPage("/BookingConfirmed", new
        {
            name = Booking.Name,
            service = Booking.Service,
            date = Booking.AppointmentDate.ToString("yyyy-MM-dd"),
            time = Booking.AppointmentTime
        });
    }
    catch (Exception ex)
    {
        ErrorMessage = ex.Message;
        return Page();
    }
    }
}

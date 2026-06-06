using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalApi.Models;
using HospitalApi.DTOs;

namespace HospitalApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly HospitalContext _context;

    public PatientsController(HospitalContext context)
    {
        _context = context;
    }

    
    // GET /api/patients
    [HttpGet]
    public async Task<IActionResult> GetPatients([FromQuery] string? search)
    {
        var query = _context.Patients
            .Include(p => p.Admissions)
                .ThenInclude(a => a.Ward)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.BedType)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Ward)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, $"%{search}%") ||
                EF.Functions.Like(p.LastName, $"%{search}%"));
        }

        var result = await query.Select(p => new
        {
            pesel = p.Pesel,
            firstName = p.FirstName,
            lastName = p.LastName,
            age = p.Age,
            sex = p.Sex,

            admissions = p.Admissions.Select(a => new
            {
                id = a.Id,
                admissionDate = a.AdmissionDate,
                dischargeDate = a.DischargeDate,
                ward = new
                {
                    id = a.Ward.Id,
                    name = a.Ward.Name,
                    description = a.Ward.Description
                }
            }),

            bedAssignments = p.BedAssignments.Select(ba => new
            {
                id = ba.Id,
                from = ba.From,
                to = ba.To,

                bed = new
                {
                    id = ba.Bed.Id,

                    bedType = new
                    {
                        id = ba.Bed.BedType.Id,
                        name = ba.Bed.BedType.Name,
                        description = ba.Bed.BedType.Description
                    },

                    room = new
                    {
                        id = ba.Bed.Room.Id,
                        hasTv = ba.Bed.Room.HasTv,

                        ward = new
                        {
                            id = ba.Bed.Room.Ward.Id,
                            name = ba.Bed.Room.Ward.Name,
                            description = ba.Bed.Room.Ward.Description
                        }
                    }
                }
            })
        }).ToListAsync();

        return Ok(result);
    }


    // POST /api/patients/{pesel}/bedassignments
    [HttpPost("{pesel}/bedassignments")]
    public async Task<IActionResult> AssignBed(string pesel, [FromBody] AssignBedRequest request)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Pesel == pesel);

        if (patient == null)
            return NotFound("Patient not found");

        if (request.To.HasValue && request.From >= request.To)
            return BadRequest("Invalid date range");

        var ward = await _context.Wards
            .FirstOrDefaultAsync(w => w.Name == request.Ward);

        if (ward == null)
            return NotFound("Ward not found");

        var bedType = await _context.BedTypes
            .FirstOrDefaultAsync(bt => bt.Name == request.BedType);

        if (bedType == null)
            return NotFound("Bed type not found");

        var bed = await _context.Beds
            .Include(b => b.Room)
            .Where(b => b.BedTypeId == bedType.Id)
            .Where(b => b.Room.WardId == ward.Id)
            .Where(b => !_context.BedAssignments.Any(ba =>
                ba.BedId == b.Id &&
                (
                    request.From < ba.To &&
                    (request.To == null || request.To > ba.From)
                )
            ))
            .FirstOrDefaultAsync();

        if (bed == null)
            return NotFound("No available bed");

        var assignment = new BedAssignment
        {
            PatientPesel = pesel,
            BedId = bed.Id,
            From = request.From,
            To = request.To
        };

        _context.BedAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Bed assigned",
            bedId = bed.Id
        });
    }
}
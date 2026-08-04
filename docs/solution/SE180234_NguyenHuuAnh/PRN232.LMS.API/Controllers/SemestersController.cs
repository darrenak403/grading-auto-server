using Microsoft.AspNetCore.Mvc;
using PRN232.LMS.API.Controllers.Helpers;
using PRN232.LMS.API.Models.Requests;
using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers;

/// <summary>Manages Semester resources</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SemestersController : ControllerBase
{
    private readonly ISemesterService _service;

    public SemestersController(ISemesterService service) => _service = service;

    /// <summary>Get all semesters with search, sort, paging, field selection and expansion</summary>
    /// <param name="search">Filter by semester name</param>
    /// <param name="sort">Sort by field (prefix - for DESC). Example: semesterName,-startDate</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 10)</param>
    /// <param name="fields">Comma-separated field names to return</param>
    /// <param name="expand">Include related data: courses</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<System.Dynamic.ExpandoObject>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? fields = null,
        [FromQuery] string? expand = null)
    {
        var query = new QueryParameters
        {
            Search = search, Sort = sort, Page = page, Size = size,
            Fields = fields, Expand = expand
        };

        var result = await _service.GetAllAsync(query);

        var responseModels = result.Items.Select(ResponseMapper.ToResponse);
        var shapedItems = DataShaper.ShapeData(responseModels, fields);

        var response = new PagedResult<System.Dynamic.ExpandoObject>
        {
            Items      = shapedItems,
            Pagination = result.Pagination
        };

        return Ok(ApiResponse<PagedResult<System.Dynamic.ExpandoObject>>.Ok(response));
    }

    /// <summary>Get semester by ID with all related courses</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<SemesterResponse>.NotFound($"Semester with id {id} not found"));

        return Ok(ApiResponse<SemesterResponse>.Ok(ResponseMapper.ToResponse(result)));
    }

    /// <summary>Create a new semester</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SemesterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SemesterResponse>.BadRequest(
                "Validation failed", ModelState));

        var model = new SemesterBusinessModel
        {
            SemesterName = request.SemesterName,
            StartDate    = request.StartDate,
            EndDate      = request.EndDate
        };

        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.SemesterId },
            ApiResponse<SemesterResponse>.Created(ResponseMapper.ToResponse(created)));
    }

    /// <summary>Update an existing semester</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<SemesterResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(int id, [FromBody] SemesterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SemesterResponse>.BadRequest(
                "Validation failed", ModelState));

        var model = new SemesterBusinessModel
        {
            SemesterName = request.SemesterName,
            StartDate    = request.StartDate,
            EndDate      = request.EndDate
        };

        var updated = await _service.UpdateAsync(id, model);
        if (updated == null)
            return NotFound(ApiResponse<SemesterResponse>.NotFound($"Semester with id {id} not found"));

        return Ok(ApiResponse<SemesterResponse>.Ok(ResponseMapper.ToResponse(updated),
            "Semester updated successfully"));
    }

    /// <summary>Delete a semester</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFound($"Semester with id {id} not found"));

        return Ok(ApiResponse<object>.Ok(new { }, "Semester deleted successfully"));
    }
}

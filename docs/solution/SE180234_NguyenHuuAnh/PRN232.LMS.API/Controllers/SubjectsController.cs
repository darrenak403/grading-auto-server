using Microsoft.AspNetCore.Mvc;
using PRN232.LMS.API.Controllers.Helpers;
using PRN232.LMS.API.Models.Requests;
using PRN232.LMS.API.Models.Responses;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.API.Controllers;

/// <summary>Manages Subject resources</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _service;

    public SubjectsController(ISubjectService service) => _service = service;

    /// <summary>Get all subjects with search, sort and paging</summary>
    /// <param name="search">Filter by subject name or code</param>
    /// <param name="sort">Sort by field (prefix - for DESC). Example: subjectName,-credit</param>
    /// <param name="page">Page number</param>
    /// <param name="size">Page size</param>
    /// <param name="fields">Comma-separated fields to return</param>
    /// <param name="expand">Not applicable for subjects</param>
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

    /// <summary>Get subject by ID</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(ApiResponse<SubjectResponse>.NotFound($"Subject with id {id} not found"));

        return Ok(ApiResponse<SubjectResponse>.Ok(ResponseMapper.ToResponse(result)));
    }

    /// <summary>Create a new subject</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] SubjectRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubjectResponse>.BadRequest("Validation failed", ModelState));

        var model = new SubjectBusinessModel
        {
            SubjectCode = request.SubjectCode,
            SubjectName = request.SubjectName,
            Credit      = request.Credit
        };

        var created = await _service.CreateAsync(model);
        return CreatedAtAction(nameof(GetById), new { id = created.SubjectId },
            ApiResponse<SubjectResponse>.Created(ResponseMapper.ToResponse(created)));
    }

    /// <summary>Update an existing subject</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SubjectResponse>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SubjectRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<SubjectResponse>.BadRequest("Validation failed", ModelState));

        var model = new SubjectBusinessModel
        {
            SubjectCode = request.SubjectCode,
            SubjectName = request.SubjectName,
            Credit      = request.Credit
        };

        var updated = await _service.UpdateAsync(id, model);
        if (updated == null)
            return NotFound(ApiResponse<SubjectResponse>.NotFound($"Subject with id {id} not found"));

        return Ok(ApiResponse<SubjectResponse>.Ok(ResponseMapper.ToResponse(updated),
            "Subject updated successfully"));
    }

    /// <summary>Delete a subject</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(ApiResponse<object>.NotFound($"Subject with id {id} not found"));

        return Ok(ApiResponse<object>.Ok(new { }, "Subject deleted successfully"));
    }
}

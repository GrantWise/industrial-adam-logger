using Industrial.Adam.Logger.Simulator.Simulation;
using Industrial.Adam.Logger.Simulator.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Industrial.Adam.Logger.Simulator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulatorController : ControllerBase
{
    private readonly SimulationEngine _simulationEngine;
    private readonly SimulatorDatabase _database;
    private readonly ILogger<SimulatorController> _logger;

    public SimulatorController(
        SimulationEngine simulationEngine,
        SimulatorDatabase database,
        ILogger<SimulatorController> logger)
    {
        _simulationEngine = simulationEngine ?? throw new ArgumentNullException(nameof(simulationEngine));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get current simulator status
    /// </summary>
    /// <returns>Current simulator statistics including production state, counters, and timing information</returns>
    /// <response code="200">Returns simulator status and statistics</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(object), 200)]
    [Produces("application/json")]
    public ActionResult<object> GetStatus()
    {
        return Ok(_simulationEngine.GetStatistics());
    }

    /// <summary>
    /// Reset a specific channel counter
    /// </summary>
    /// <param name="channel">Channel number (0-15)</param>
    /// <returns>Confirmation message</returns>
    /// <response code="200">Channel counter reset successfully</response>
    /// <response code="400">Invalid channel number (must be 0-15)</response>
    [HttpPost("channels/{channel}/reset")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [Produces("application/json")]
    public ActionResult ResetCounter(int channel)
    {
        if (channel < 0 || channel >= 16)
        {
            return BadRequest("Invalid channel number");
        }

        _simulationEngine.ResetChannel(channel);
        _logger.LogInformation("Channel {Channel} counter reset", channel);

        return Ok(new { message = $"Channel {channel} counter reset" });
    }

    /// <summary>
    /// Force a production stoppage
    /// </summary>
    /// <param name="request">Stoppage request with type (minor/major) and optional reason</param>
    /// <returns>Confirmation message</returns>
    /// <response code="200">Production stoppage initiated successfully</response>
    /// <response code="400">Invalid stoppage type (must be 'minor' or 'major')</response>
    [HttpPost("production/force-stoppage")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [Produces("application/json")]
    [Consumes("application/json")]
    public async Task<ActionResult> ForceStoppage([FromBody] StoppageRequest request)
    {
        if (request.Type != "minor" && request.Type != "major")
        {
            return BadRequest("Stoppage type must be 'minor' or 'major'");
        }

        var stoppageType = request.Type == "minor"
            ? ProductionState.MinorStoppage
            : ProductionState.MajorStoppage;

        _simulationEngine.ForceStoppage(stoppageType);

        // Record event
        await _database.RecordEventAsync(
            "SIM001", // TODO: Get from config
            null,
            $"Forced {request.Type} stoppage",
            null,
            request.Reason);

        _logger.LogInformation("Forced {Type} stoppage: {Reason}", request.Type, request.Reason);

        return Ok(new { message = $"Forced {request.Type} stoppage" });
    }

    /// <summary>
    /// Start a new production job
    /// </summary>
    /// <param name="request">Optional job request with name and target quantity</param>
    /// <returns>Confirmation message</returns>
    /// <response code="200">New production job started successfully</response>
    [HttpPost("production/start-job")]
    [ProducesResponseType(typeof(object), 200)]
    [Produces("application/json")]
    [Consumes("application/json")]
    public async Task<ActionResult> StartNewJob([FromBody] JobRequest? request = null)
    {
        _simulationEngine.StartNewJob();

        // Record event
        await _database.RecordEventAsync(
            "SIM001",
            null,
            "New job started",
            null,
            request?.JobName);

        _logger.LogInformation("Started new job: {JobName}", request?.JobName ?? "unnamed");

        return Ok(new { message = "New job started" });
    }

    /// <summary>
    /// Get production history
    /// </summary>
    /// <param name="hours">Number of hours of history to retrieve (default: 24)</param>
    /// <returns>List of production events from the specified time period</returns>
    /// <response code="200">Returns production history events</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ProductionEvent>), 200)]
    [Produces("application/json")]
    public async Task<ActionResult<List<ProductionEvent>>> GetHistory([FromQuery] int hours = 24)
    {
        var events = await _database.GetRecentEventsAsync("SIM001", hours);
        return Ok(events);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status of the simulator</returns>
    /// <response code="200">Returns simulator health status and current production state</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    [Produces("application/json")]
    public ActionResult<object> HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            ProductionState = _simulationEngine.GetProductionState().ToString()
        });
    }
}

/// <summary>
/// Request model for forcing a production stoppage
/// </summary>
public class StoppageRequest
{
    /// <summary>
    /// Type of stoppage ('minor' or 'major')
    /// </summary>
    public string Type { get; set; } = "minor";

    /// <summary>
    /// Optional reason for the stoppage
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Request model for starting a new production job
/// </summary>
public class JobRequest
{
    /// <summary>
    /// Optional name for the production job
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// Optional target quantity for the job
    /// </summary>
    public int? TargetQuantity { get; set; }
}

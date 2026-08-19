1	#nullable enable
2	// =============================================================================
3	// Author: Vladyslav Zaiets | https://sarmkadan.com
4	// CTO & Software Architect
5	// =============================================================================
6
7	namespace SarmKadan.DistributedLock.Api.Controller;
8
9	using Microsoft.AspNetCore.Mvc;
10	using SarmKadan.DistributedLock.Core.Repository;
11
12	/// <summary>
13	/// Health check endpoints for monitoring the distributed lock system.
14	/// Used by load balancers and orchestration platforms to verify service health.
15	/// </summary>
16	[ApiController]
17	[Route("api/health")]
18	[Produces("application/json")]
19	public sealed class HealthCheckController : ControllerBase
20	{
21	    private readonly ILockRepository _repository;
22	    private readonly ILogger<HealthCheckController> _logger;
23
24	    public HealthCheckController(ILockRepository repository, ILogger<HealthCheckController> logger)
25	    {
26	        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
27	        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
28	    }
29
30	    /// <summary>
31	    /// Performs a liveness check - indicates whether the service is running.
32	    /// </summary>
33	    [HttpGet("live")]
34	    [ProducesResponseType(StatusCodes.Status200OK)]
35	    public ActionResult<HealthCheckResponse> Liveness()
36	    {
37	        _logger.LogInformation("Liveness check initiated", new { });
38	        try
39	        {
40	            return Ok("healthy".ToHealthCheckResponse());
41	        }
42	        catch (Exception ex)
43	        {
44	            _logger.LogError(ex, "Liveness check failed - unexpected error");
45	            return StatusCode(
46	                StatusCodes.Status500InternalServerError,
47	                "error".ToHealthCheckResponse(new HealthDetails { ErrorMessage = ex.Message }));
48	        }
49	    }
50
51	    /// <summary>
52	    /// Performs a readiness check - indicates whether the service can accept requests.
53	    /// Verifies connectivity to the lock backend (Redis, PostgreSQL, SQLite, etc).
54	    /// </summary>
55	    [HttpGet("ready")]
56	    [ProducesResponseType(StatusCodes.Status200OK)]
57	    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
58	    public async Task<ActionResult<HealthCheckResponse>> Readiness()
59	    {
60	        _logger.LogInformation("Readiness check initiated", new { });
61	        try
62	        {
63	            var testLock = await _repository.GetLockAsync("__health_check__");
64	            _logger.LogInformation("Readiness check passed", new { });
65	            var details = new HealthDetails { BackendConnected = true };
66	            return Ok("ready".ToHealthCheckResponse(details));
67	        }
68	        catch (Exception ex)
69	        {
70	            _logger.LogError(ex, "Readiness check failed - backend connectivity issue");
71	            var details = new HealthDetails
72	            {
73	                BackendConnected = false,
74	                ErrorMessage = ex.Message
75	            };
76	            return StatusCode(
77	                StatusCodes.Status503ServiceUnavailable,
78	                "not_ready".ToHealthCheckResponse(details));
79	        }
80	    }
81
82	    /// <summary>
83	    /// Detailed health status including metrics and backend information.
84	    /// </summary>
85	    [HttpGet("detailed")]
86	    [ProducesResponseType(StatusCodes.Status200OK)]
87	    public async Task<ActionResult<DetailedHealthResponse>> DetailedHealth()
88	    {
89	        _logger.LogInformation("Detailed health check initiated", new { });
90	        var startTime = DateTime.UtcNow;
91	        try
92	        {
93	            var backendHealthy = await VerifyBackendConnectivity();
94	            _logger.LogInformation("Detailed health check passed", new { });
95	            var responseTime = DateTime.UtcNow.Subtract(startTime);
96	            return Ok(backendHealthy.ToDetailedHealthResponse((long)responseTime.TotalMilliseconds));
97	        }
98	        catch (Exception ex)
99	        {
100	            _logger.LogError(ex, "Detailed health check failed - unexpected error");
101	            return StatusCode(
102	                StatusCodes.Status500InternalServerError,
103	                "error".ToDetailedHealthResponse(new RuntimeInfo { Framework = "dotnet", Uptime = DateTime.UtcNow - startTime }));
104	        }
105	    }
106
107	    private async Task<bool> VerifyBackendConnectivity()
108	    {
109	        try
110	        {
111	            await _repository.GetLockAsync("__health_check__");
112	            return true;
113	        }
114	        catch
115	        {
116	            return false;
117	        }
118	    }
119
120	    private static string GetAssemblyVersion()
121	    {
122	        return typeof(HealthCheckController).Assembly
123	            .GetName()
124	            .Version?.ToString() ?? "1.0.0";
125	    }
126	}
127
128	public record HealthCheckResponse
129	{
130	    public required string Status { get; init; }
131	    public DateTime Timestamp { get; init; }
132	    public string Version { get; init; } = string.Empty;
133	    public HealthDetails? Details { get; init; }
134	}
135
136	public record HealthDetails
137	{
138	    public bool BackendConnected { get; init; }
139	    public string? ErrorMessage { get; init; }
140	}
141
142	public record DetailedHealthResponse
143	{
144	    public required string Status { get; init; }
145	    public DateTime Timestamp { get; init; }
146	    public string Version { get; init; } = string.Empty;
147	    public long ResponseTimeMs { get; init; }
148	    public RuntimeInfo? Runtime { get; init; }
149	}
150
151	public record RuntimeInfo
152	{
153	    public string Framework { get; init; } = string.Empty;
154	    public TimeSpan Uptime { get; init; }
155	}
156
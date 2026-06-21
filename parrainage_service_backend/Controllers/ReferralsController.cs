using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using ParrainageBackend.Data;

using ParrainageBackend.Dto;

using ParrainageBackend.Services;



namespace ParrainageBackend.Controllers;



[ApiController]

[Route("api/parrainage/referrals")]

public sealed class ReferralsController(

    ParrainageDbContext db,

    ReferralWorkflowService workflow,

    ReferralEligibilityService eligibility,

    ReferralRuleResolver ruleResolver,

    ReferralCvStorageService cvStorage,

    IParrainageRequestUserResolver userResolver) : ControllerBase

{

    [HttpGet]

    public async Task<ActionResult<List<ReferralDto>>> List(CancellationToken ct)

    {

        await eligibility.ProcessEligibleReferralsAsync(ct);

        var rows = await db.Referrals.AsNoTracking()

            .OrderByDescending(r => r.CreatedAt)

            .ToListAsync(ct);

        return Ok(rows.Select(r => r.ToDto()).ToList());

    }



    [HttpGet("history")]

    public async Task<ActionResult<List<ReferralHistoryDto>>> History(CancellationToken ct)

    {

        var rows = await db.ReferralHistory.AsNoTracking()

            .OrderByDescending(h => h.CreatedAt)

            .ToListAsync(ct);

        return Ok(rows.Select(h => h.ToDto()).ToList());

    }



    [HttpGet("{id}")]

    public async Task<ActionResult<ReferralDto>> GetById(string id, CancellationToken ct)

    {

        await eligibility.ProcessEligibleReferralsAsync(ct);

        var entity = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

        return Ok(entity.ToDto());

    }



    [HttpGet("{id}/reward-preview")]

    public async Task<ActionResult<ReferralRewardPreviewDto>> RewardPreview(string id, CancellationToken ct)

    {

        var entity = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

        var defaults = await ruleResolver.ResolveRewardDefaultsAsync(entity, ct);

        return Ok(new ReferralRewardPreviewDto

        {

            SuggestedAmount = defaults.SuggestedAmount,

            MinDurationMonths = defaults.MinDurationMonths,

            RuleLabel = defaults.RuleLabel,

            AppliedRuleId = entity.AppliedRuleId,

            PositionMode = entity.PositionMode,

        });

    }



    [HttpPost]

    public async Task<ActionResult<ReferralDto>> Create([FromBody] CreateReferralRequest body, CancellationToken ct)

    {

        if (string.IsNullOrWhiteSpace(body.CandidateName) || string.IsNullOrWhiteSpace(body.CandidateEmail))

            return BadRequest(new { error = "candidateName et candidateEmail sont requis." });



        try

        {

            var created = await workflow.SubmitReferralAsync(body, ct);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPatch("{id}")]

    public async Task<ActionResult<ReferralDto>> Update(string id, [FromBody] UpdateReferralRequest body, CancellationToken ct)

    {

        var entity = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });



        if (body.ReferrerName != null) entity.ReferrerName = body.ReferrerName;

        if (body.ProjectName != null) entity.ProjectName = body.ProjectName;

        if (body.CandidateName != null) entity.CandidateName = body.CandidateName;

        if (body.CandidateEmail != null) entity.CandidateEmail = body.CandidateEmail;

        if (body.CandidatePhone != null) entity.CandidatePhone = body.CandidatePhone;

        if (body.Position != null) entity.Position = body.Position;

        if (body.CvUrl != null) entity.CvUrl = body.CvUrl;

        if (body.Status != null)

        {

            var allowed = new[] { "SUBMITTED", "PROCESSED", "IN_TRAINING", "APPROVED", "REJECTED", "REWARDED" };

            if (!allowed.Contains(body.Status))

                return BadRequest(new { error = "status invalide (SUBMITTED|PROCESSED|IN_TRAINING|APPROVED|REJECTED|REWARDED)." });

            if (body.Status == "APPROVED")

                return BadRequest(new { error = "Utilisez POST /approve pour valider un dossier." });

            entity.Status = body.Status;

        }

        if (body.RewardAmount.HasValue) entity.RewardAmount = body.RewardAmount.Value;



        var manualPatch = body.Status != null || body.RewardAmount.HasValue

            || body.CandidateName != null || body.CandidateEmail != null;

        if (manualPatch)

        {

            db.AuditLogs.Add(new AuditLogEntryEntity

            {

                Id = $"audit-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",

                Action = "REFERRAL_MANUAL_UPDATE",

                UserId = body.Actor?.Id ?? "admin-1",

                UserLabel = body.Actor?.Label ?? "Administrateur",

                Timestamp = DateTimeOffset.UtcNow,

                Details = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),

            });

        }



        await db.SaveChangesAsync(ct);

        return Ok(entity.ToDto());

    }



    [HttpPost("{id}/process")]

    public async Task<ActionResult<ReferralDto>> Process(string id, [FromBody] ProcessReferralRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.ProcessReferralAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/approve")]

    public async Task<ActionResult<ReferralDto>> Approve(string id, [FromBody] ApproveReferralRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.ApproveReferralAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/confirm-production")]

    public async Task<ActionResult<ReferralDto>> ConfirmProduction(string id, [FromBody] ConfirmProductionStartRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.ConfirmProductionStartAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/reject-early-departure")]

    public async Task<ActionResult<ReferralDto>> RejectEarlyDeparture(string id, [FromBody] RejectEarlyDepartureRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.RejectEarlyDepartureAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/extend-training")]

    public async Task<ActionResult<ReferralDto>> ExtendTraining(string id, [FromBody] ExtendTrainingRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.ExtendTrainingAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/confirm-eligibility")]

    public async Task<ActionResult<ReferralDto>> ConfirmEligibility(

        string id,

        [FromBody] ConfirmPaymentEligibilityRequest body,

        CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.IsRh(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.ConfirmPaymentEligibilityAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/status")]

    public async Task<ActionResult<ReferralDto>> ChangeStatus(string id, [FromBody] UpdateStatusRequest body, CancellationToken ct)

    {

        var allowed = new[] { "SUBMITTED", "REJECTED" };

        if (!allowed.Contains(body.Status))

            return BadRequest(new { error = "status invalide pour cet endpoint (SUBMITTED|REJECTED)." });



        try

        {

            var updated = await workflow.UpdateStatusAsync(id, body.Status, body.Actor, body.Comment, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/reward")]

    public async Task<ActionResult<ReferralDto>> Reward(string id, [FromBody] RewardRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))

            return Forbid();



        try

        {

            var updated = await workflow.MarkReferralPaidAsync(

                id,

                new MarkReferralPaymentRequest

                {

                    Paid = true,

                    PaidAt = DateTimeOffset.UtcNow,

                    Actor = body.Actor ?? new ActorDto { Id = user.UserId, Label = user.Role },

                },

                ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/payment")]

    public async Task<ActionResult<ReferralDto>> Payment(string id, [FromBody] MarkReferralPaymentRequest body, CancellationToken ct)

    {

        var user = userResolver.Resolve(Request);

        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))

            return Forbid();



        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };



        try

        {

            var updated = await workflow.MarkReferralPaidAsync(id, body, ct);

            if (updated == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });

            return Ok(updated.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpPost("{id}/cv")]

    [RequestSizeLimit(10 * 1024 * 1024)]

    public async Task<ActionResult<ReferralDto>> UploadCv(string id, IFormFile file, CancellationToken ct)

    {

        var entity = await db.Referrals.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });



        try

        {

            entity.CvUrl = await cvStorage.SaveAsync(id, file, ct);

            await db.SaveChangesAsync(ct);

            return Ok(entity.ToDto());

        }

        catch (InvalidOperationException ex)

        {

            return BadRequest(new { error = ex.Message });

        }

    }



    [HttpGet("{id}/cv")]

    public async Task<IActionResult> DownloadCv(string id, [FromQuery] string? disposition, CancellationToken ct)

    {

        var entity = await db.Referrals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

        if (entity == null) return NotFound(new { error = $"Parrainage introuvable : {id}" });



        var opened = cvStorage.OpenRead(id);

        if (opened == null) return NotFound(new { error = "CV introuvable pour ce parrainage." });



        var (stream, contentType, fileName) = opened.Value;

        var inline = string.Equals(disposition, "inline", StringComparison.OrdinalIgnoreCase);

        if (inline)

        {

            Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";

            return File(stream, contentType, enableRangeProcessing: true);

        }



        return File(stream, contentType, fileName, enableRangeProcessing: true);

    }

}



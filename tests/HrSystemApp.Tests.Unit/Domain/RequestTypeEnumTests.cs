using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Resources;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Tests.Unit.Domain;

/// <summary>
/// Tests for the RequestType enum additions (Survey = 11, Complaint = 12)
/// and the DomainErrors.Attendance.InvalidClockIn error added in this PR.
/// </summary>
public class RequestTypeEnumTests
{
    // ─── New enum values ──────────────────────────────────────────────────────

    [Fact]
    public void RequestType_Survey_HasValue11()
    {
        ((int)RequestType.Survey).Should().Be(11);
    }

    [Fact]
    public void RequestType_Complaint_HasValue12()
    {
        ((int)RequestType.Complaint).Should().Be(12);
    }

    [Fact]
    public void RequestType_Survey_ParsesFromIntCorrectly()
    {
        var parsed = (RequestType)11;
        parsed.Should().Be(RequestType.Survey);
    }

    [Fact]
    public void RequestType_Complaint_ParsesFromIntCorrectly()
    {
        var parsed = (RequestType)12;
        parsed.Should().Be(RequestType.Complaint);
    }

    [Fact]
    public void RequestType_Survey_ToStringReturnsName()
    {
        RequestType.Survey.ToString().Should().Be("Survey");
    }

    [Fact]
    public void RequestType_Complaint_ToStringReturnsName()
    {
        RequestType.Complaint.ToString().Should().Be("Complaint");
    }

    [Fact]
    public void RequestType_ExistingValuesAreNotAffected()
    {
        // Ensure the new values did not shift existing ones.
        ((int)RequestType.Leave).Should().Be(0);
        ((int)RequestType.Permission).Should().Be(1);
        ((int)RequestType.Other).Should().Be(10);
    }

    [Fact]
    public void RequestType_HasExpectedTotalCount()
    {
        // Leave(0)…Other(10) = 11 values, plus Survey(11) and Complaint(12) = 13 total.
        var allValues = Enum.GetValues<RequestType>();
        allValues.Should().HaveCount(13);
    }

    // ─── DomainErrors.Attendance.InvalidClockIn ───────────────────────────────

    [Fact]
    public void DomainErrors_Attendance_InvalidClockIn_HasCorrectCode()
    {
        DomainErrors.Attendance.InvalidClockIn.Code.Should().Be("Attendance.InvalidClockIn");
    }

    [Fact]
    public void DomainErrors_Attendance_InvalidClockIn_HasNonEmptyMessage()
    {
        DomainErrors.Attendance.InvalidClockIn.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DomainErrors_Attendance_InvalidClockIn_MessageMatchesMessagesClass()
    {
        DomainErrors.Attendance.InvalidClockIn.Message.Should().Be(Messages.Errors.InvalidClockIn);
    }

    [Fact]
    public void DomainErrors_Attendance_InvalidClockOut_StillExists()
    {
        // Regression: adding InvalidClockIn must not remove or rename InvalidClockOut.
        DomainErrors.Attendance.InvalidClockOut.Code.Should().Be("Attendance.InvalidClockOut");
    }

    [Fact]
    public void DomainErrors_Attendance_InvalidClockIn_IsDifferentFromInvalidClockOut()
    {
        DomainErrors.Attendance.InvalidClockIn.Should()
            .NotBe(DomainErrors.Attendance.InvalidClockOut);
    }

    [Fact]
    public void Messages_Errors_InvalidClockIn_HasCorrectText()
    {
        Messages.Errors.InvalidClockIn.Should()
            .Be("Clock-in time cannot be more than 5 minutes in the future.");
    }
}
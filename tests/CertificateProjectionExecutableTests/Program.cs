using StackMeet.Api.Services;

static void Expect(string name, string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"{name}: expected '{expected}', got '{actual}'.");
    Console.WriteLine($"PASS {name}: {actual}");
}

const string actualState = "{\"divisionSettings\":{\"male\":[12],\"female\":[12],\"combined\":[]},\"settings\":{\"start\":\"2026-07-11\",\"ageCalculationMode\":\"actual\"}}";
const string yearBornState = "{\"divisionSettings\":{\"male\":[12],\"female\":[12],\"combined\":[]},\"settings\":{\"start\":\"2026-07-11\",\"ageCalculationMode\":\"yearBorn\"}}";
const string combinedState = "{\"divisionSettings\":{\"male\":[10],\"combined\":[10]},\"settings\":{\"start\":\"2026-07-11\"}}";
const string specialState = "{\"divisionSettings\":{\"special\":[12]},\"settings\":{\"start\":\"2026-07-11\",\"separateSpecialDivisionsByGender\":true}}";

Expect("actual age", ParticipantCertificateProjectionService.ResolveDivisionValues(null, new DateOnly(2014, 7, 12), new DateOnly(2026, 7, 11), "M", false, actualState), "12 & Under Male");
Expect("year born", ParticipantCertificateProjectionService.ResolveDivisionValues(null, new DateOnly(2014, 7, 12), new DateOnly(2026, 7, 11), "M", false, yearBornState), "12 & Under Male");
Expect("combined precedence", ParticipantCertificateProjectionService.ResolveDivisionValues(null, new DateOnly(2016, 7, 11), new DateOnly(2026, 7, 11), "M", false, combinedState), "10 & Under Combined");
Expect("special female", ParticipantCertificateProjectionService.ResolveDivisionValues(null, new DateOnly(2014, 7, 11), new DateOnly(2026, 7, 11), "F", true, specialState), "SS 12 & Under L1 F");
Expect("custom division", ParticipantCertificateProjectionService.ResolveDivisionValues("Invitational", null, new DateOnly(2026, 7, 11), "M", false, actualState), "Invitational");
Expect("missing DOB", ParticipantCertificateProjectionService.ResolveDivisionValues(null, null, new DateOnly(2026, 7, 11), "M", false, actualState), "Open / Unassigned");
Console.WriteLine("Certificate projection executable tests passed.");

using Bogus;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using FluentAssertions;

namespace CashFlow.UnitTests.Ledger.Validators;

/// <summary>
/// Unit tests para o validador LedgerEntryValidator
/// Testes estressados focados especificamente na validação de regras de negócio
/// </summary>
public class LedgerEntryValidatorTests
{
    private readonly LedgerEntryValidator _validator;

    public LedgerEntryValidatorTests()
    {
        _validator = new LedgerEntryValidator();
    }

    #region Valid Scenarios

    [Fact]
    public void ValidateCompleteEntry_WithAllValidFields_ShouldSucceed()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 500m,
            occurredAtUtc: DateTime.UtcNow.AddDays(-5),
            description: "Valid transaction",
            idempotencyKey: Guid.NewGuid().ToString()
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateEntry_WithNullDescription_ShouldSucceed()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Debit,
            amount: 100.75m,
            occurredAtUtc: DateTime.UtcNow,
            description: null,
            idempotencyKey: "idempotency-key-123"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntry_WithEmptyDescription_ShouldSucceed()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 250.50m,
            occurredAtUtc: DateTime.UtcNow.AddDays(-1),
            description: "",
            idempotencyKey: Guid.NewGuid().ToString()
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(LedgerEntryType.Credit)]
    [InlineData(LedgerEntryType.Debit)]
    public void ValidateEntry_WithBothValidTypes_ShouldSucceed(LedgerEntryType type)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: type,
            amount: 1000m,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test-key"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Invalid Scenarios - Individual Field Failures

    [Fact]
    public void ValidateEntry_WithEmptyMerchantId_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.Empty,
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(LedgerEntry.MerchantId));
    }

    [Fact]
    public void ValidateEntry_WithInvalidType_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: (LedgerEntryType)999,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Type));
    }

    [Fact]
    public void ValidateEntry_WithZeroAmount_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 0,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Amount));
    }

    [Fact]
    public void ValidateEntry_WithNegativeAmount_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: -50.25m,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Amount));
    }

    [Fact]
    public void ValidateEntry_WithFutureOccurredAt_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow.AddHours(1),
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.OccurredAtUtc));
    }

    [Fact]
    public void ValidateEntry_WithDescriptionExceeding500Chars_ShouldFail()
    {
        // Arrange
        var longDescription = new string('a', 501);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: longDescription,
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Description));
    }

    [Fact]
    public void ValidateEntry_WithEmptyIdempotencyKey_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: ""
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.IdempotencyKey));
    }

    [Fact]
    public void ValidateEntry_WithIdempotencyKeyExceeding256Chars_ShouldFail()
    {
        // Arrange
        var longKey = new string('x', 257);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: longKey
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.IdempotencyKey));
    }

    #endregion

    #region Multiple Error Scenarios

    [Fact]
    public void ValidateEntry_WithMultipleInvalidFields_ShouldReportAllErrors()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.Empty,
            type: (LedgerEntryType)999,
            amount: -100,
            occurredAtUtc: DateTime.UtcNow.AddDays(1),
            description: new string('a', 501),
            idempotencyKey: ""
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void ValidateEntry_WithPartialInvalidFields_ShouldReportOnlyInvalidOnes()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: -50,
            occurredAtUtc: DateTime.UtcNow,
            description: "Valid description",
            idempotencyKey: "valid-key"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(LedgerEntry.Amount));
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public void ValidateEntry_WithMinimalPositiveAmount_ShouldPass()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 0.001m,
            occurredAtUtc: DateTime.UtcNow,
            description: "Minimal amount",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntry_WithMaximumDescription_ShouldPass()
    {
        // Arrange
        var maxDescription = new string('a', 500);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: maxDescription,
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateEntry_WithMaximumIdempotencyKey_ShouldPass()
    {
        // Arrange
        var maxKey = new string('x', 256);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: maxKey
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    [InlineData(3650)]
    public void ValidateEntry_WithVariousPastDateOffsets_ShouldPass(int daysInPast)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow.AddDays(-daysInPast),
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var result = _validator.Validate(entry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Stress Tests with Bogus

    [Fact]
    public void ValidateMultipleRandomEntries_AllValidData_ShouldPassAll()
    {
        // Arrange
        var faker = new Faker<LedgerEntry>()
            .CustomInstantiator(f => new LedgerEntry(
                merchantId: Guid.NewGuid(),
                type: f.PickRandom<LedgerEntryType>(),
                amount: f.Random.Decimal(0.01m, 10000),
                occurredAtUtc: f.Date.PastDateOnly(365).ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
                description: f.Random.Bool() ? f.Commerce.ProductDescription() : null,
                idempotencyKey: Guid.NewGuid().ToString()
            ));

        var entries = faker.Generate(100);

        // Act & Assert
        foreach (var entry in entries)
        {
            var result = _validator.Validate(entry);
            result.IsValid.Should().BeTrue($"Entry {entry.Id} should be valid");
        }
    }

    [Fact]
    public void ValidateRandomEntries_WithRandomInvalidData_ShouldIdentifyErrors()
    {
        // Arrange
        var random = new Random(42);
        var testCount = 100;
        var invalidCount = 0;

        // Act
        for (int i = 0; i < testCount; i++)
        {
            var description = random.NextDouble() > 0.9 ? new string('a', 501) : Guid.NewGuid().ToString();
            if (description.Length > 50)
                description = description[..50];

            var entry = new LedgerEntry(
                merchantId: random.NextDouble() > 0.8 ? Guid.Empty : Guid.NewGuid(),
                type: (LedgerEntryType)(random.Next(0, 10) > 7 ? 999 : random.Next(1, 3)),
                amount: random.NextDouble() > 0.7 ? (decimal)(random.NextDouble() - 0.5) * 100 : (decimal)(random.NextDouble() * 10000 + 0.01),
                occurredAtUtc: random.NextDouble() > 0.85 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                description: description,
                idempotencyKey: random.NextDouble() > 0.8 ? "" : Guid.NewGuid().ToString()
            );

            var result = _validator.Validate(entry);
            if (!result.IsValid)
            {
                invalidCount++;
            }
        }

        // Assert
        invalidCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateEntries_WithAllFieldVariations_ShouldHandleCorrectly()
    {
        // Arrange
        var results = new List<bool>();
        var faker = new Faker();

        // Test various combinations
        var merchantIds = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
        var types = new[] { LedgerEntryType.Credit, LedgerEntryType.Debit };
        var amounts = new[] { -100m, 0m, 0.01m, 100m };
        var descriptions = new[] { null, "", "Valid" };
        var keys = new[] { "", "valid-key", new string('x', 256) };

        // Act & Assert
        var validCount = 0;
        var invalidCount = 0;

        foreach (var merchantId in merchantIds)
        {
            foreach (var type in types)
            {
                foreach (var amount in amounts.Take(3))
                {
                    foreach (var description in descriptions.Take(2))
                    {
                        foreach (var key in keys.Take(2))
                        {
                            var entry = new LedgerEntry(
                                merchantId: merchantId,
                                type: type,
                                amount: amount,
                                occurredAtUtc: DateTime.UtcNow,
                                description: description,
                                idempotencyKey: key
                            );

                            var result = _validator.Validate(entry);
                            if (result.IsValid)
                                validCount++;
                            else
                                invalidCount++;
                        }
                    }
                }
            }
        }

        validCount.Should().BeGreaterThan(0);
        invalidCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void ValidateLargeNumberOfEntries_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var faker = new Faker<LedgerEntry>()
            .CustomInstantiator(f => new LedgerEntry(
                merchantId: Guid.NewGuid(),
                type: f.PickRandom<LedgerEntryType>(),
                amount: f.Random.Decimal(0.01m, 10000),
                occurredAtUtc: f.Date.PastDateOnly(365).ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
                description: f.Commerce.ProductDescription(),
                idempotencyKey: Guid.NewGuid().ToString()
            ));

        var entries = faker.Generate(10000);
        var startTime = DateTime.UtcNow;

        // Act
        var validCount = 0;
        foreach (var entry in entries)
        {
            if (_validator.Validate(entry).IsValid)
                validCount++;
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert
        validCount.Should().Be(10000);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    #endregion
}

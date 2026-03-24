using Bogus;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using FluentAssertions;
using FluentValidation;

namespace CashFlow.UnitTests.Ledger;

/// <summary>
/// Unit tests para a entidade LedgerEntry
/// Testes estressados com múltiplos cenários usando Bogus para geração de dados
/// </summary>
public class LedgerEntryTests
{
    private readonly Faker<LedgerEntry> _faker;
    private readonly LedgerEntryValidator _validator;

    public LedgerEntryTests()
    {
        _validator = new LedgerEntryValidator();
        
        _faker = new Faker<LedgerEntry>()
            .CustomInstantiator(f => new LedgerEntry(
                merchantId: Guid.NewGuid(),
                type: f.PickRandom<LedgerEntryType>(),
                amount: f.Random.Decimal(0.01m, 10000),
                occurredAtUtc: f.Date.PastDateOnly(30).ToDateTime(TimeOnly.MinValue).ToUniversalTime(),
                description: f.Commerce.ProductDescription(),
                idempotencyKey: Guid.NewGuid().ToString()
            ));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var type = LedgerEntryType.Credit;
        var amount = 100.50m;
        var occurredAtUtc = DateTime.UtcNow.AddDays(-1);
        var description = "Test transaction";
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act
        var ledgerEntry = new LedgerEntry(
            merchantId: merchantId,
            type: type,
            amount: amount,
            occurredAtUtc: occurredAtUtc,
            description: description,
            idempotencyKey: idempotencyKey
        );

        // Assert
        ledgerEntry.MerchantId.Should().Be(merchantId);
        ledgerEntry.Type.Should().Be(type);
        ledgerEntry.Amount.Should().Be(amount);
        ledgerEntry.OccurredAtUtc.Should().Be(occurredAtUtc.ToUniversalTime());
        ledgerEntry.Description.Should().Be(description);
        ledgerEntry.IdempotencyKey.Should().Be(idempotencyKey);
        ledgerEntry.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullDescription_ShouldAccept()
    {
        // Arrange & Act
        var ledgerEntry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Debit,
            amount: 50.25m,
            occurredAtUtc: DateTime.UtcNow,
            description: null,
            idempotencyKey: "key123"
        );

        // Assert
        ledgerEntry.Description.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithWhitespaceIdempotencyKey_ShouldTrim()
    {
        // Arrange
        var keyWithWhitespace = "  test-key-123  ";

        // Act
        var ledgerEntry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: keyWithWhitespace
        );

        // Assert
        ledgerEntry.IdempotencyKey.Should().Be("test-key-123");
    }

    [Fact]
    public void Constructor_ShouldSetOccurredAtUtcToUtcKind()
    {
        // Arrange
        var localTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);

        // Act
        var ledgerEntry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: localTime,
            description: "Test",
            idempotencyKey: "test"
        );

        // Assert
        ledgerEntry.OccurredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIdForEachInstance()
    {
        // Arrange & Act
        var entry1 = _faker.Generate();
        var entry2 = _faker.Generate();
        var entry3 = _faker.Generate();

        // Assert
        entry1.Id.Should().NotBe(entry2.Id);
        entry2.Id.Should().NotBe(entry3.Id);
        entry1.Id.Should().NotBe(entry3.Id);
    }

    #endregion

    #region Type Validation Tests

    [Theory]
    [InlineData(LedgerEntryType.Credit)]
    [InlineData(LedgerEntryType.Debit)]
    public void Constructor_WithValidType_ShouldAccept(LedgerEntryType type)
    {
        // Act
        var ledgerEntry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: type,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Assert
        ledgerEntry.Type.Should().Be(type);
    }

    [Fact]
    public void Validator_WithInvalidType_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: (LedgerEntryType)999, // Invalid type
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Type));
    }

    #endregion

    #region Amount Validation Tests

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100.50)]
    [InlineData(10000)]
    public void Validator_WithValidAmounts_ShouldPass(decimal amount)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: amount,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Validator_WithInvalidAmounts_ShouldFail(decimal amount)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: amount,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Amount));
    }

    [Fact]
    public void Validator_WithVerySmallAmount_ShouldPass()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 0.001m,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithVeryLargeAmount_ShouldPass()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: (decimal)Math.Pow(10, 10),
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region MerchantId Validation Tests

    [Fact]
    public void Validator_WithEmptyMerchantId_ShouldFail()
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
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.MerchantId));
    }

    [Fact]
    public void Validator_WithValidMerchantId_ShouldPass()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region OccurredAtUtc Validation Tests

    [Fact]
    public void Validator_WithFutureOccurredAt_ShouldFail()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: futureDate,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.OccurredAtUtc));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public void Validator_WithPastOccurredAt_ShouldPass(int daysInPast)
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-daysInPast);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: pastDate,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithCurrentOccurredAt_ShouldPass()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: nowUtc,
            description: "Test",
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Description Validation Tests

    [Theory]
    [InlineData("")]
    [InlineData("Short description")]
    [InlineData("A description with 100 characters: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor.")]
    public void Validator_WithValidDescriptions_ShouldPass(string description)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: description,
            idempotencyKey: "test"
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithDescriptionExceeding500Characters_ShouldFail()
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
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.Description));
    }

    [Fact]
    public void Validator_WithMaximumLengthDescription_ShouldPass()
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
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region IdempotencyKey Validation Tests

    [Fact]
    public void Validator_WithEmptyIdempotencyKey_ShouldFail()
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
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.IdempotencyKey));
    }

    [Fact]
    public void Validator_WithOnlyWhitespaceIdempotencyKey_ShouldFail()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: "   "
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_WithValidIdempotencyKey_ShouldPass()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: Guid.NewGuid().ToString()
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithMaximumLengthIdempotencyKey_ShouldPass()
    {
        // Arrange
        var maxKeyLength = new string('a', 256);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: maxKeyLength
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithIdempotencyKeyExceeding256Characters_ShouldFail()
    {
        // Arrange
        var tooLongKey = new string('a', 257);
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: tooLongKey
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(LedgerEntry.IdempotencyKey));
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void Validator_WithCompleteValidData_ShouldPass()
    {
        // Arrange
        var entry = _faker.Generate();

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GenerateMultipleEntries_AllShouldHaveUniqueIds()
    {
        // Arrange & Act
        var entries = _faker.Generate(100);

        // Assert
        entries.Select(e => e.Id).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void GenerateMultipleEntries_AllShouldPassValidation()
    {
        // Arrange & Act
        var entries = _faker.Generate(50);

        // Assert
        foreach (var entry in entries)
        {
            var result = _validator.Validate(entry);
            result.IsValid.Should().BeTrue($"Entry {entry.Id} should be valid");
        }
    }

    [Fact]
    public void Constructor_WithMultipleMerchantsAndTypes_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var entries = new List<LedgerEntry>();
        var merchantIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var types = new[] { LedgerEntryType.Credit, LedgerEntryType.Debit };

        foreach (var merchantId in merchantIds)
        {
            foreach (var type in types)
            {
                var entry = new LedgerEntry(
                    merchantId: merchantId,
                    type: type,
                    amount: new Faker().Random.Decimal(1, 1000),
                    occurredAtUtc: DateTime.UtcNow.AddDays(-new Faker().Random.Int(1, 30)),
                    description: new Faker().Commerce.ProductDescription(),
                    idempotencyKey: Guid.NewGuid().ToString()
                );
                entries.Add(entry);
            }
        }

        // Assert
        entries.Should().HaveCount(10);
        entries.GroupBy(e => e.MerchantId).Should().HaveCount(5);
    }

    [Fact]
    public void MultipleValidations_ShouldFindAllErrors()
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.Empty, // Invalid
            type: (LedgerEntryType)999, // Invalid
            amount: -100, // Invalid
            occurredAtUtc: DateTime.UtcNow.AddDays(1), // Invalid (future)
            description: new string('a', 501), // Invalid (too long)
            idempotencyKey: "" // Invalid (empty)
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().HaveCountGreaterThan(1);
    }

    #endregion

    #region Edge Cases and Stress Tests

    [Theory]
    [InlineData("single-key")]
    [InlineData("key-with-dashes-and-numbers-123")]
    [InlineData("key_with_underscores")]
    [InlineData("key.with.dots")]
    [InlineData("MixedCaseKey")]
    public void Validator_WithVariousIdempotencyKeyFormats_ShouldPass(string key)
    {
        // Arrange
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: DateTime.UtcNow,
            description: "Test",
            idempotencyKey: key
        );

        // Act
        var validationResult = _validator.Validate(entry);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithVeryOldDate_ShouldAccept()
    {
        // Arrange
        var veryOldDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var entry = new LedgerEntry(
            merchantId: Guid.NewGuid(),
            type: LedgerEntryType.Credit,
            amount: 100,
            occurredAtUtc: veryOldDate,
            description: "Test",
            idempotencyKey: "test"
        );

        // Assert
        entry.OccurredAtUtc.Should().Be(veryOldDate);
    }

    [Fact]
    public void Validator_GenerateRandomizedEntries_StressTest()
    {
        // Arrange
        var random = new Random(42);
        var testCount = 1000;

        // Act & Assert
        for (int i = 0; i < testCount; i++)
        {
            var entry = new LedgerEntry(
                merchantId: Guid.NewGuid(),
                type: random.Next(0, 2) == 0 ? LedgerEntryType.Credit : LedgerEntryType.Debit,
                amount: (decimal)random.NextDouble() * 10000 + 0.01m,
                occurredAtUtc: DateTime.UtcNow.AddDays(-random.Next(0, 365)),
                description: i % 10 == 0 ? null : $"Transaction {i}",
                idempotencyKey: $"key-{Guid.NewGuid()}"
            );

            var result = _validator.Validate(entry);
            result.IsValid.Should().BeTrue($"Entry iteration {i} should be valid");
        }
    }

    #endregion
}

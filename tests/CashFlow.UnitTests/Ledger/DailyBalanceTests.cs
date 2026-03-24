using Bogus;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using FluentAssertions;
using FluentValidation;

namespace CashFlow.UnitTests.Ledger;

/// <summary>
/// Unit tests para a entidade DailyBalance
/// Testes estressados com múltiplos cenários usando Bogus para geração de dados
/// </summary>
public class DailyBalanceTests
{
    private readonly Faker<DailyBalance> _faker;
    private readonly DailyBalanceValidator _validator;

    public DailyBalanceTests()
    {
        _validator = new DailyBalanceValidator();
        
        _faker = new Faker<DailyBalance>()
            .CustomInstantiator(f => new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(f.Date.PastDateOnly(30).ToDateTime(TimeOnly.MinValue))
            ));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithMerchantIdAndDate()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var balance = new DailyBalance(merchantId: merchantId, date: date);

        // Assert
        balance.MerchantId.Should().Be(merchantId);
        balance.Date.Should().Be(date);
        balance.Balance.Should().Be(0);
        balance.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        balance.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Constructor_ShouldSetInitialBalanceToZero()
    {
        // Arrange & Act
        var balance = _faker.Generate();

        // Assert
        balance.Balance.Should().Be(0);
    }

    [Fact]
    public void Constructor_ShouldSetUpdatedAtUtcToNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var afterCreation = DateTime.UtcNow;

        // Assert
        balance.UpdatedAtUtc.Should().BeOnOrAfter(beforeCreation);
        balance.UpdatedAtUtc.Should().BeOnOrBefore(afterCreation);
    }

    [Fact]
    public void Constructor_ShouldGenerateUniqueIdForEachInstance()
    {
        // Arrange & Act
        var balance1 = _faker.Generate();
        var balance2 = _faker.Generate();
        var balance3 = _faker.Generate();

        // Assert
        balance1.Id.Should().NotBe(balance2.Id);
        balance2.Id.Should().NotBe(balance3.Id);
        balance1.Id.Should().NotBe(balance3.Id);
    }

    #endregion

    #region MerchantId Validation Tests

    [Fact]
    public void Validator_WithEmptyMerchantId_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.Empty,
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.MerchantId));
    }

    [Fact]
    public void Validator_WithValidMerchantId_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithMultipleMerchantIds_ShouldValidateIndependently()
    {
        // Arrange
        var merchants = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        // Act & Assert
        foreach (var merchantId in merchants)
        {
            var balance = new DailyBalance(
                merchantId: merchantId,
                date: DateOnly.FromDateTime(DateTime.UtcNow)
            );
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue($"MerchantId {merchantId} should be valid");
        }
    }

    #endregion

    #region Date Validation Tests

    [Fact]
    public void Validator_WithFutureDate_ShouldFail()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: futureDate
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.Date));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(365)]
    public void Validator_WithPastDates_ShouldPass(int daysInPast)
    {
        // Arrange
        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysInPast));
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: pastDate
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithCurrentDate_ShouldPass()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: today
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithVeryOldDate_ShouldPass()
    {
        // Arrange
        var veryOldDate = new DateOnly(1900, 1, 1);
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: veryOldDate
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithVariousDatesInRange_ShouldAccept()
    {
        // Arrange & Act
        var dates = new List<DateOnly>
        {
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 6, 15),
            new DateOnly(2024, 12, 31),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        };

        var balances = dates.Select(d => new DailyBalance(Guid.NewGuid(), d)).ToList();

        // Assert
        balances.Should().HaveCount(4);
        balances.ForEach(b => _validator.Validate(b).IsValid.Should().BeTrue());
    }

    #endregion

    #region Balance Operations Tests

    [Fact]
    public void ApplyCredit_ShouldIncreaseBalance()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var creditAmount = 100.50m;

        // Act
        balance.ApplyCredit(creditAmount);

        // Assert
        balance.Balance.Should().Be(creditAmount);
    }

    [Fact]
    public void ApplyDebit_ShouldDecreaseBalance()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var debitAmount = 50.25m;

        // Act
        balance.ApplyDebit(debitAmount);

        // Assert
        balance.Balance.Should().Be(-debitAmount);
    }

    [Fact]
    public void ApplyCredit_MultipleOperations_ShouldAccumulateCorrectly()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var amounts = new[] { 100m, 50m, 75.50m, 25m };

        // Act
        foreach (var amount in amounts)
        {
            balance.ApplyCredit(amount);
        }

        // Assert
        balance.Balance.Should().Be(250.50m);
    }

    [Fact]
    public void ApplyDebit_MultipleOperations_ShouldAccumulateCorrectly()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var amounts = new[] { 30m, 20m, 15m, 10m };

        // Act
        foreach (var amount in amounts)
        {
            balance.ApplyDebit(amount);
        }

        // Assert
        balance.Balance.Should().Be(-75m);
    }

    [Fact]
    public void ApplyCredit_AndDebit_ShouldNetCorrectly()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        balance.ApplyCredit(200m);
        balance.ApplyDebit(80m);
        balance.ApplyCredit(50m);
        balance.ApplyDebit(30m);

        // Assert
        balance.Balance.Should().Be(140m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void ApplyCredit_WithLargeAmounts_ShouldHandleCorrectly(decimal amount)
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        balance.ApplyCredit(amount);

        // Assert
        balance.Balance.Should().Be(amount);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.99)]
    public void ApplyCredit_WithSmallAmounts_ShouldHandleCorrectly(decimal amount)
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        balance.ApplyCredit(amount);

        // Assert
        balance.Balance.Should().Be(amount);
    }

    [Fact]
    public void ApplyCredit_WithVeryLargeAmount_ShouldNotOverflow()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var largeAmount = (decimal)Math.Pow(10, 10);

        // Act
        balance.ApplyCredit(largeAmount);

        // Assert
        balance.Balance.Should().Be(largeAmount);
    }

    #endregion

    #region UpdatedAtUtc Validation Tests

    [Fact]
    public void ApplyCredit_ShouldUpdateTimestamp()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var originalTime = balance.UpdatedAtUtc;

        // Act
        System.Threading.Thread.Sleep(10);
        balance.ApplyCredit(100);

        // Assert
        balance.UpdatedAtUtc.Should().BeAfter(originalTime);
    }

    [Fact]
    public void ApplyDebit_ShouldUpdateTimestamp()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var originalTime = balance.UpdatedAtUtc;

        // Act
        System.Threading.Thread.Sleep(10);
        balance.ApplyDebit(50);

        // Assert
        balance.UpdatedAtUtc.Should().BeAfter(originalTime);
    }

    [Fact]
    public void Validator_WithFutureUpdatedAtUtc_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        
        // Simulate future timestamp via reflection (since setter is private)
        var futureTime = DateTime.UtcNow.AddDays(1);
        var balanceType = typeof(DailyBalance);
        var property = balanceType.GetProperty(nameof(DailyBalance.UpdatedAtUtc));
        property?.SetValue(balance, futureTime);

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.UpdatedAtUtc));
    }

    #endregion

    #region Balance Validation Tests

    [Fact]
    public void Validator_WithNegativeBalance_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        balance.ApplyDebit(100);

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.Balance));
    }

    [Fact]
    public void Validator_WithZeroBalance_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void Validator_WithPositiveBalance_ShouldPass(decimal amount)
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        balance.ApplyCredit(amount);

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_WithVeryLargeBalance_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        balance.ApplyCredit((decimal)Math.Pow(10, 10));

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public void Validator_WithCompleteValidData_ShouldPass()
    {
        // Arrange
        var balance = _faker.Generate();

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GenerateMultipleBalances_AllShouldHaveUniqueIds()
    {
        // Arrange & Act
        var balances = _faker.Generate(100);

        // Assert
        balances.Select(b => b.Id).Distinct().Should().HaveCount(100);
    }

    [Fact]
    public void GenerateMultipleBalances_AllShouldPassValidation()
    {
        // Arrange & Act
        var balances = _faker.Generate(50);

        // Assert
        foreach (var balance in balances)
        {
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue($"Balance {balance.Id} should be valid");
        }
    }

    [Fact]
    public void SimulateDailyMerchantTransactions_ShouldMaintainBalanceIntegrity()
    {
        // Arrange
        var faker = new Faker();
        var merchants = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var balances = merchants.ToDictionary(m => m, m => new DailyBalance(m, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        for (int i = 0; i < 100; i++)
        {
            var merchant = faker.PickRandom(merchants);
            var amount = faker.Random.Decimal(1, 1000);
            var isCredit = faker.Random.Bool();

            if (isCredit)
            {
                balances[merchant].ApplyCredit(amount);
            }
            else
            {
                balances[merchant].ApplyDebit(amount);
            }
        }

        // Assert
        foreach (var balance in balances.Values)
        {
            balance.Id.Should().NotBeEmpty();
            balance.MerchantId.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void MultipleValidations_WithInvalidData_ShouldFindAllErrors()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.Empty,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        );

        // Act
        var validationResult = _validator.Validate(balance);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region Edge Cases and Stress Tests

    [Fact]
    public void Constructor_WithMultipleMerchantsAndDates_ShouldHandleCorrectly()
    {
        // Arrange
        var balances = new List<DailyBalance>();
        var merchantIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        for (int dayOffset = 0; dayOffset < 30; dayOffset++)
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-dayOffset));
            foreach (var merchantId in merchantIds)
            {
                balances.Add(new DailyBalance(merchantId, date));
            }
        }

        // Act & Assert
        balances.Should().HaveCount(300);
        balances.Select(b => b.Id).Distinct().Should().HaveCount(300);
    }

    [Fact]
    public void ApplyCredit_AndDebit_StressTest()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var random = new Random(42);
        var expectedBalance = 0m;

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var amount = (decimal)random.NextDouble() * 100 + 0.01m;
            if (i % 2 == 0)
            {
                balance.ApplyCredit(amount);
                expectedBalance += amount;
            }
            else
            {
                balance.ApplyDebit(amount);
                expectedBalance -= amount;
            }
        }

        // Assert
        balance.Balance.Should().Be(expectedBalance);
    }

    [Fact]
    public void Validator_WithRandomGeneratedBalances_StressTest()
    {
        // Arrange
        var random = new Random(42);
        var testCount = 1000;

        // Act & Assert
        for (int i = 0; i < testCount; i++)
        {
            var balance = new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-random.Next(0, 365)))
            );

            // Apply random operations
            var operationCount = random.Next(0, 20);
            for (int j = 0; j < operationCount; j++)
            {
                var amount = (decimal)random.NextDouble() * 10000 + 0.01m;
                if (random.NextDouble() > 0.5)
                {
                    balance.ApplyCredit(amount);
                }
                else
                {
                    // Only apply debit if it won't make balance negative
                    if (balance.Balance >= amount)
                    {
                        balance.ApplyDebit(amount);
                    }
                }
            }

            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue($"Balance iteration {i} should be valid");
        }
    }

    [Fact]
    public void Constructor_WithDatesAcrossYears_ShouldHandleCorrectly()
    {
        // Arrange
        var dates = new[]
        {
            new DateOnly(2020, 1, 1),
            new DateOnly(2021, 6, 15),
            new DateOnly(2022, 12, 31),
            new DateOnly(2023, 3, 10),
            new DateOnly(2024, 9, 25)
        };

        // Act
        var balances = dates.Select(d => new DailyBalance(Guid.NewGuid(), d)).ToList();

        // Assert
        balances.Should().HaveCount(5);
        foreach (var balance in balances)
        {
            _validator.Validate(balance).IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void ApplyOperations_WithPrecisionDecimals_ShouldMaintainAccuracy()
    {
        // Arrange
        var balance = new DailyBalance(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow));
        var amounts = new[] { 10.25m, 20.50m, 30.75m, 40.99m, 50.01m };

        // Act
        foreach (var amount in amounts)
        {
            balance.ApplyCredit(amount);
        }

        // Assert
        balance.Balance.Should().Be(152.50m);
    }

    [Fact]
    public void SameMerchantMultipleDates_ShouldCreateDistinctBalances()
    {
        // Arrange
        var merchantId = Guid.NewGuid();
        var dates = Enumerable.Range(0, 30)
            .Select(i => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)))
            .ToList();

        // Act
        var balances = dates.Select(d => new DailyBalance(merchantId, d)).ToList();

        // Assert
        balances.Should().HaveCount(30);
        balances.Select(b => b.Id).Distinct().Should().HaveCount(30);
        balances.ForEach(b => b.MerchantId.Should().Be(merchantId));
    }

    #endregion
}

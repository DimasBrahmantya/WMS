using FluentAssertions;
using Wms.Application.Common.Models;
using Xunit;

namespace Wms.Application.Tests.Common;

/// <summary>
/// Model representasi state UI terpadu untuk komponen Blazor / Web.
/// Menjamin konsistensi rendering: Loading, Empty, Error, dan Success.
/// </summary>
public enum UiViewStateType
{
    Loading,
    Empty,
    Error,
    Success
}

public class UiViewState<T>
{
    public UiViewStateType StateType { get; private set; }
    public T? Data { get; private set; }
    public string? Message { get; private set; }
    public bool CanRetry { get; private set; }
    public bool IsLoading => StateType == UiViewStateType.Loading;
    public bool IsEmpty => StateType == UiViewStateType.Empty;
    public bool IsError => StateType == UiViewStateType.Error;
    public bool IsSuccess => StateType == UiViewStateType.Success;

    private UiViewState() { }

    public static UiViewState<T> Loading(string message = "Memuat data...") =>
        new() { StateType = UiViewStateType.Loading, Message = message };

    public static UiViewState<T> Empty(string message = "Tidak ada data yang ditemukan.") =>
        new() { StateType = UiViewStateType.Empty, Message = message };

    public static UiViewState<T> Error(string message, bool canRetry = true) =>
        new() { StateType = UiViewStateType.Error, Message = SanitizeErrorMessage(message), CanRetry = canRetry };

    public static UiViewState<T> Success(T data, string? message = null) =>
        new() { StateType = UiViewStateType.Success, Data = data, Message = message };

    private static string SanitizeErrorMessage(string raw)
    {
        // Standar keamanan: jangan tampilkan stack trace atau kredensial database di UI
        if (raw.Contains("at ") || raw.Contains("Password=") || raw.Contains("Server="))
        {
            return "Terjadi kesalahan internal pada server. Silakan coba beberapa saat lagi.";
        }
        return raw;
    }
}

public class UiStateHandlingTests
{
    [Fact]
    public void LoadingState_ShouldSetFlagsCorrectly()
    {
        var state = UiViewState<List<string>>.Loading("Memuat daftar gudang...");

        state.IsLoading.Should().BeTrue();
        state.IsEmpty.Should().BeFalse();
        state.IsError.Should().BeFalse();
        state.IsSuccess.Should().BeFalse();
        state.Message.Should().Be("Memuat daftar gudang...");
        state.Data.Should().BeNull();
    }

    [Fact]
    public void EmptyState_ShouldIndicateNoDataAvailable()
    {
        var state = UiViewState<List<string>>.Empty("Belum ada mutasi barang pada periode ini.");

        state.IsLoading.Should().BeFalse();
        state.IsEmpty.Should().BeTrue();
        state.IsError.Should().BeFalse();
        state.IsSuccess.Should().BeFalse();
        state.Message.Should().Be("Belum ada mutasi barang pada periode ini.");
        state.Data.Should().BeNull();
    }

    [Fact]
    public void ErrorState_ShouldMaskSensitiveServerDetails_AndAllowRetry()
    {
        var rawSensitiveError = "Npgsql.NpgsqlException: at Npgsql.Internal.NpgsqlConnector.Open() Password=secret123 Server=db.supabase.co";
        var state = UiViewState<string>.Error(rawSensitiveError, canRetry: true);

        state.IsLoading.Should().BeFalse();
        state.IsEmpty.Should().BeFalse();
        state.IsError.Should().BeTrue();
        state.IsSuccess.Should().BeFalse();
        state.CanRetry.Should().BeTrue();

        // Verifikasi error masking / data hygiene
        state.Message.Should().NotContain("secret123");
        state.Message.Should().NotContain("Npgsql");
        state.Message.Should().Be("Terjadi kesalahan internal pada server. Silakan coba beberapa saat lagi.");
    }

    [Fact]
    public void SuccessState_ShouldContainDataAndSuccessMessage()
    {
        var sampleData = new List<string> { "WH-01", "WH-02" };
        var state = UiViewState<List<string>>.Success(sampleData, "Data gudang berhasil dimuat.");

        state.IsLoading.Should().BeFalse();
        state.IsEmpty.Should().BeFalse();
        state.IsError.Should().BeFalse();
        state.IsSuccess.Should().BeTrue();
        state.Data.Should().BeEquivalentTo(sampleData);
        state.Message.Should().Be("Data gudang berhasil dimuat.");
    }

    [Fact]
    public void ResultToUiState_TransitionCycle_ShouldFlowDeterministically()
    {
        // 1. Initial State = Loading
        var state = UiViewState<PaginatedList<string>>.Loading();
        state.IsLoading.Should().BeTrue();

        // 2. Fetch returned empty
        var emptyList = new PaginatedList<string>(new List<string>(), 0, 1, 10);
        if (emptyList.TotalCount == 0)
        {
            state = UiViewState<PaginatedList<string>>.Empty("Tidak ada barang ditemukan.");
        }
        state.IsEmpty.Should().BeTrue();

        // 3. User created an item -> returned Success
        var populatedList = new PaginatedList<string>(new List<string> { "SKU-001" }, 1, 1, 10);
        state = UiViewState<PaginatedList<string>>.Success(populatedList, "Berhasil memuat barang.");
        state.IsSuccess.Should().BeTrue();
        state.Data!.Items.Should().ContainSingle();

        // 4. Server error occurred
        var failResult = Result.Failure("Koneksi terputus.");
        if (!failResult.Succeeded)
        {
            state = UiViewState<PaginatedList<string>>.Error(failResult.ErrorMessage ?? "Error");
        }
        state.IsError.Should().BeTrue();
        state.Message.Should().Be("Koneksi terputus.");
    }
}


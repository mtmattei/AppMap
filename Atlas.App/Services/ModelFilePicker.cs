using Windows.Storage;
using Windows.Storage.Pickers;

namespace Atlas.App.Services;

/// <summary>FileOpenPicker-based implementation; dispatches to the UI thread because
/// MVUX commands may execute off it.</summary>
public sealed class ModelFilePicker : IModelFilePicker
{
    public async Task<string?> PickModelJsonAsync(CancellationToken ct)
    {
        var dispatcher = App.MainDispatcher
            ?? throw new InvalidOperationException("Dispatcher not available before the window exists.");

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.TryEnqueue(async () =>
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder,
                };
                picker.FileTypeFilter.Add(".json");

                var file = await picker.PickSingleFileAsync();
                completion.TrySetResult(file is null ? null : await FileIO.ReadTextAsync(file));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return await completion.Task.WaitAsync(ct);
    }
}

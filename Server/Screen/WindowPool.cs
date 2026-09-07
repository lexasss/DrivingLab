namespace Server.Screen;

internal class WindowPool
{
    public WindowPool(int? poolSize = null)
    {
        _poolSize = poolSize ?? _poolSize;

        for (int i = 0; i < _poolSize; i++)
        {
            var window = new MediaWindow();
            _pool[window.Id] = window;
        }
    }

    public MediaWindow Obtain()
    {
        if (_pool.Count == 0)
        {
            return new MediaWindow();
        }

        var (id, result) = _pool.First();
        _pool.Remove(id);

        Task.Run(() =>
        {
            var window = new MediaWindow();
            _pool[window.Id] = window;
        });

        return result;
    }

    #region Internal

    readonly int _poolSize = 4;

    Dictionary<string, MediaWindow> _pool = [];

    #endregion
}

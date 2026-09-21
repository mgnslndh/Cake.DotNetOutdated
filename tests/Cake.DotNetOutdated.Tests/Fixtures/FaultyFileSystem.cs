using Cake.Core.IO;

namespace Cake.DotNetOutdated.Tests.Fixtures;

/// <summary>
/// Decorates a file system and makes deleting or reading selected files fail with an <see cref="IOException"/>.
/// </summary>
internal sealed class FaultyFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;

    public FaultyFileSystem(IFileSystem inner)
    {
        _inner = inner;
    }

    /// <summary>
    /// Gets the full paths of the files whose <see cref="IFile.Delete"/> throws.
    /// </summary>
    public HashSet<string> FailOnDelete { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the full paths of the files that throw when they are opened for reading.
    /// </summary>
    public HashSet<string> FailOnRead { get; } = new HashSet<string>(StringComparer.Ordinal);

    public IFile GetFile(FilePath path) => new FaultyFile(this, _inner.GetFile(path));

    public IDirectory GetDirectory(DirectoryPath path) => _inner.GetDirectory(path);

    private sealed class FaultyFile : IFile
    {
        private readonly FaultyFileSystem _owner;
        private readonly IFile _inner;

        public FaultyFile(FaultyFileSystem owner, IFile inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public FilePath Path => _inner.Path;

        Cake.Core.IO.Path IFileSystemInfo.Path => _inner.Path;

        public bool Exists => _inner.Exists;

        public bool Hidden => _inner.Hidden;

        public DateTime? LastWriteTimeUtc => _inner.LastWriteTimeUtc;

        public DateTime? CreationTimeUtc => _inner.CreationTimeUtc;

        public DateTime? LastAccessTimeUtc => _inner.LastAccessTimeUtc;

        public UnixFileMode? UnixFileMode => _inner.UnixFileMode;

        public long Length => _inner.Length;

        public FileAttributes Attributes
        {
            get => _inner.Attributes;
            set => _inner.Attributes = value;
        }

        public void Copy(FilePath destination, bool overwrite) => _inner.Copy(destination, overwrite);

        public void Move(FilePath destination) => _inner.Move(destination);

        public void Delete()
        {
            if (_owner.FailOnDelete.Contains(_inner.Path.FullPath))
            {
                throw new IOException($"The process cannot access the file '{_inner.Path.FullPath}' because it is being used by another process.");
            }

            _inner.Delete();
        }

        public Stream Open(FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
            if (fileAccess == FileAccess.Read && _owner.FailOnRead.Contains(_inner.Path.FullPath))
            {
                throw new IOException($"Could not read '{_inner.Path.FullPath}'.");
            }

            return _inner.Open(fileMode, fileAccess, fileShare);
        }

        public IFile SetCreationTime(DateTime creationTime)
        {
            _inner.SetCreationTime(creationTime);
            return this;
        }

        public IFile SetCreationTimeUtc(DateTime creationTimeUtc)
        {
            _inner.SetCreationTimeUtc(creationTimeUtc);
            return this;
        }

        public IFile SetLastAccessTime(DateTime lastAccessTime)
        {
            _inner.SetLastAccessTime(lastAccessTime);
            return this;
        }

        public IFile SetLastAccessTimeUtc(DateTime lastAccessTimeUtc)
        {
            _inner.SetLastAccessTimeUtc(lastAccessTimeUtc);
            return this;
        }

        public IFile SetLastWriteTime(DateTime lastWriteTime)
        {
            _inner.SetLastWriteTime(lastWriteTime);
            return this;
        }

        public IFile SetLastWriteTimeUtc(DateTime lastWriteTimeUtc)
        {
            _inner.SetLastWriteTimeUtc(lastWriteTimeUtc);
            return this;
        }

        public IFile SetUnixFileMode(UnixFileMode mode)
        {
            _inner.SetUnixFileMode(mode);
            return this;
        }
    }
}

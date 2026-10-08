using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using HellMudGateway.Base.Types;

namespace HellMudGateway.Client.Infras.Components;

public sealed class ClientConnection : IRawConnection
{
    private readonly bool CompressDisabled;
	private readonly TcpClient _telnetConnection;
	private readonly NetworkStream _stream;
	private readonly CancellationTokenSource _cancellation = new();
	private int _closed;

	public ClientConnection(TcpClient telnetConnection, ConnectionPort local,bool compressDisabled)
	{
		ArgumentNullException.ThrowIfNull(telnetConnection);

		_telnetConnection = telnetConnection;
		CompressDisabled = compressDisabled;
		if (!telnetConnection.Connected ||
			telnetConnection.Client.RemoteEndPoint is not IPEndPoint remoteEndPoint ||
			telnetConnection.Client.LocalEndPoint is not IPEndPoint localEndPoint)
		{
			throw new ArgumentException("The Telnet TCP connection must be connected.", nameof(telnetConnection));
		}

		_stream = telnetConnection.GetStream();
		RemoteAddress = remoteEndPoint.Address.ToString();
		RemotePort = remoteEndPoint.Port;
		Local = local;

		AddressType = remoteEndPoint.Address.AddressFamily == AddressFamily.InterNetworkV6
			? RawConnectionAdressType.V6
			: RawConnectionAdressType.V4;

		Input = Channel.CreateUnbounded<TelnetData>();
		Output = Channel.CreateUnbounded<TelnetData>();
		_ = ReadFromConnectionAsync();
		_ = WriteToConnectionAsync();
	}

	public string RemoteAddress { get; }
	public int RemotePort { get; }
	public ConnectionPort Local { get; }
	public RawConnectionAdressType AddressType { get; }
	public Channel<TelnetData> Input { get; }
	public Channel<TelnetData> Output { get; }

	public void Close()
	{
		if (Interlocked.Exchange(ref _closed, 1) != 0)
		{
			return;
		}

		_cancellation.Cancel();
		Input.Writer.TryComplete();
		Output.Writer.TryComplete();
		_telnetConnection.Dispose();
	}

	private async Task ReadFromConnectionAsync()
	{
		var buffer = new byte[8192];
		Exception? error = null;

		try
		{
			while (!_cancellation.IsCancellationRequested)
			{
				var bytesRead = await _stream.ReadAsync(buffer, _cancellation.Token);
				if (bytesRead == 0)
				{
					break;
				}

				await Input.Writer.WriteAsync(new TelnetData(TelnetDataType.Data, buffer[..bytesRead], ConntectionCharset.UTF8, 0, null), _cancellation.Token);
			}
		}
		catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
		{
		}
		catch (IOException exception)
		{
			error = exception;
		}
		catch (SocketException exception)
		{
			error = exception;
		}
		catch (ObjectDisposedException) when (_cancellation.IsCancellationRequested)
		{
		}
		finally
		{
			Input.Writer.TryComplete(error);
		}
	}

	private async Task WriteToConnectionAsync()
	{
		try
		{
			await foreach (var data in Output.Reader.ReadAllAsync(_cancellation.Token))
			{
				await _stream.WriteAsync(data.Data, _cancellation.Token);
			}
		}
		catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
		{
		}
		catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
		{
			if (!_cancellation.IsCancellationRequested)
			{
				Close();
				Output.Reader.Completion.ContinueWith(_ => { }, TaskScheduler.Default);
			}
		}
	}
}

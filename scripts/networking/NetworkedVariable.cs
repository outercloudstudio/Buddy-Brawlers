using System;
using Godot;
using Riptide;

namespace Networking
{
  public class NetworkedVariable<ValueType>
  {
    public enum Authority
    {
      Server,
      Client
    }

    public enum UpdateEvent
    {
      Change,
      Manual
    }

    private ValueType _value;
    public ValueType Value
    {
      get
      {
        return _value;
      }

      set
      {
        Synced = true;

        if (_updateEvent == UpdateEvent.Change && !_value.Equals(value)) SendUpdate();

        _value = value;
      }
    }

    public bool Synced = false;

    private string _name;
    private NetworkPointUser _source;
    private Authority _authority;
    private UpdateEvent _updateEvent;
    private MessageSendMode _messageSendMode;
    private uint _minimumSendDelay;
    private ulong _lastSentTick;
    private int _lastRecievedIndex = -1;
    private int _lastSentIndex = -1;

    public NetworkedVariable(ValueType intitalValue, uint minimumSendDelay = 30, Authority authority = Authority.Client, UpdateEvent updateEvent = UpdateEvent.Manual, MessageSendMode messageSendMode = MessageSendMode.Unreliable)
    {
      _value = intitalValue;
      _minimumSendDelay = minimumSendDelay;
      _authority = authority;
      _updateEvent = updateEvent;
      _messageSendMode = messageSendMode;

      // if (NetworkManager.SafeMode && _minimumSendDelay < 50) _minimumSendDelay = 50;
      if (_minimumSendDelay < 50) _minimumSendDelay = 50;
    }

    public void Register(NetworkPointUser source, string name)
    {
      _source = source;
      _name = name;
    }

    public void Sync()
    {
      SendUpdate();
    }

    private Action<Message> SetupMessage(bool propogate)
    {
      return (Message message) =>
      {
        message.AddBool(propogate);

        _lastSentIndex++;

        message.AddInt(_lastSentIndex);

        if (typeof(ValueType) == typeof(int))
        {
          message.AddInt((int)(object)_value);
        }

        if (typeof(ValueType) == typeof(float))
        {
          message.AddFloat((float)(object)_value);
        }

        if (typeof(ValueType) == typeof(Vector2))
        {
          Vector2 castedValue = (Vector2)(object)_value;
          message.AddFloat(castedValue.X);
          message.AddFloat(castedValue.Y);
        }

        if (typeof(ValueType) == typeof(Vector3))
        {
          Vector3 castedValue = (Vector3)(object)_value;
          message.AddFloat(castedValue.X);
          message.AddFloat(castedValue.Y);
          message.AddFloat(castedValue.Z);
        }
      };
    }

    private void SendUpdate()
    {
      if (_source == null) throw new Exception("Can not send updates for a networked variable that has not been setup!");

      if (!_source.NetworkPoint.IsOwner) return;

      ulong now = Time.GetTicksMsec();

      if (now - _lastSentTick < _minimumSendDelay) return;

      _lastSentTick = now;

      if (_authority == Authority.Server)
      {
        NetworkManager.SendRpcToClients(_source, _name, SetupMessage(false), _messageSendMode);
      }

      if (_authority == Authority.Client)
      {
        NetworkManager.SendRpcToServer(_source, _name, SetupMessage(true), _messageSendMode);
      }
    }

    public void ReceiveUpdate(Message message)
    {
      bool propogate = message.GetBool();

      int index = message.GetInt();

      if (index <= _lastRecievedIndex) return;

      _lastRecievedIndex = index;
      _lastSentIndex = Math.Max(_lastSentIndex, _lastRecievedIndex);

      Synced = true;

      if (!(_authority == Authority.Client && _source.GetMultiplayerAuthority() == NetworkManager.LocalClient.Id) && !(_authority == Authority.Server && NetworkManager.IsHost))
      {
        if (typeof(ValueType) == typeof(int))
        {
          _value = (ValueType)(object)message.GetInt();
        }

        if (typeof(ValueType) == typeof(float))
        {
          _value = (ValueType)(object)message.GetFloat();
        }

        if (typeof(ValueType) == typeof(Vector2))
        {
          _value = (ValueType)(object)new Vector2(message.GetFloat(), message.GetFloat());
        }

        if (typeof(ValueType) == typeof(Vector3))
        {
          _value = (ValueType)(object)new Vector3(message.GetFloat(), message.GetFloat(), message.GetFloat());
        }
      }

      if (propogate) NetworkManager.SendRpcToClients(_source, _name, SetupMessage(false), _messageSendMode);
    }
  }
}
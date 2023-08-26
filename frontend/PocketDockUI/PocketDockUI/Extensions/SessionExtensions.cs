namespace PocketDockUI.Extensions;

public static class SessionExtensions
{
    public static void SetKey(this ISession session, SessionKey key, string value)
    {
        session.SetString(key.ToString(), value);
    }

    public static string GetKey(this ISession session, SessionKey key)
    {
        return session.GetString(key.ToString());
    }

    public static void RemoveKey(this ISession session, SessionKey key)
    {
        session.Remove(key.ToString());
    }
    
    public static string GetBanner(this ISession session)
    {
        var message = session.GetKey(SessionKey.Message);
        session.RemoveKey(SessionKey.Message);
        return message;
    }

    public static void AddBanner(this ISession session, string value)
    {
        session.SetKey(SessionKey.Message, value);
    }
}

public enum SessionKey
{
    UserId,
    Message
}
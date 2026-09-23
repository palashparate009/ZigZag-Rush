using UnityEditor;
using UnityEngine;

public static class LoginScreenTestTools
{
    private const string LoginScreenTestKey = "ShowLoginScreenForTest";

    [MenuItem("ZigZag/Testing/Show New User Login On Next Play")]
    private static void ShowNewUserLoginOnNextPlay()
    {
        PlayerPrefs.SetInt(LoginScreenTestKey, 1);
        PlayerPrefs.Save();
        Debug.Log("The new-user login page will be shown on the next Play. Existing progress was not deleted.");
    }

    [MenuItem("ZigZag/Testing/Reset Special Block FTUE")]
    private static void ResetSpecialBlockFtue()
    {
        PlayerPrefs.DeleteKey("FtueSeenMask");
        PlayerPrefs.Save();
        Debug.Log("Special-block FTUE progress was reset. Each block tooltip will appear again before the car reaches it.");
    }
}

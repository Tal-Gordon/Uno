using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Profile : MonoBehaviour
{
    public TMP_InputField usernameInputField;

    public static string username;
    public static int pictureInt;

    private readonly string defaultUsername = "Guest";
    void Awake()
    {
        username = defaultUsername;
    }

    public void OnEndEdit()
    {
        username = usernameInputField.text;
    }
}
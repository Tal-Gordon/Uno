using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AIplayer : MonoBehaviour
{
    public Player player;

    private Card GetCardToPlay()
    {
        List<Card> deck = player.GetDeck();

        return null; // Remove later
    }
}

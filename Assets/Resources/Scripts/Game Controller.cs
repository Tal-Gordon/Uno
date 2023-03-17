using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<Player> players = new();
    [SerializeField] Card topCard;
    
    public static Dictionary<string, ((int, CardColor), int)> gameCards = new();

    private readonly string playerStandsName = "Player stands";
    private readonly string discardPileName = "Discard pile";
    private void Awake()
    {
        gameCards = new Dictionary<string, ((int, CardColor), int)> // Card name identifier, card internal representation, available amount
        {
            {"Red 1", ((1, CardColor.Red), 2)},
            {"Red 2", ((2, CardColor.Red) , 2)},
            {"Red 3", ((3, CardColor.Red) , 2)},
            {"Red 4", ((4, CardColor.Red) , 2)},
            {"Red 5", ((5, CardColor.Red) , 2)},
            {"Red 6", ((6, CardColor.Red) , 2)},
            {"Red 7", ((7, CardColor.Red) , 2)},
            {"Red 8", ((8, CardColor.Red) , 2)},
            {"Red 9", ((9, CardColor.Red) , 2)},
            {"Red 0", ((10, CardColor.Red) , 1)},
            {"Red Draw Two", ((11, CardColor.Red) , 2)},
            {"Red Skip", ((12, CardColor.Red) , 2)},
            {"Red Reverse", ((13, CardColor.Red) , 2)},

            {"Blue 1", ((1, CardColor.Blue), 2)},
            {"Blue 2", ((2, CardColor.Blue), 2)},
            {"Blue 3", ((3, CardColor.Blue), 2)},
            {"Blue 4", ((4, CardColor.Blue), 2)},
            {"Blue 5", ((5, CardColor.Blue), 2)},
            {"Blue 6", ((6, CardColor.Blue), 2)},
            {"Blue 7", ((7, CardColor.Blue), 2)},
            {"Blue 8", ((8, CardColor.Blue), 2)},
            {"Blue 9", ((9, CardColor.Blue), 2)},
            {"Blue 0", ((10, CardColor.Blue), 1)},
            {"Blue Draw Two", ((11, CardColor.Blue), 2)},
            {"Blue Skip", ((12, CardColor.Blue), 2)},
            {"Blue Reverse", ((13, CardColor.Blue), 2)},

            {"Green 1", ((1, CardColor.Green), 2)},
            {"Green 2", ((2, CardColor.Green), 2)},
            {"Green 3", ((3, CardColor.Green), 2)},
            {"Green 4", ((4, CardColor.Green), 2)},
            {"Green 5", ((5, CardColor.Green), 2)},
            {"Green 6", ((6, CardColor.Green), 2)},
            {"Green 7", ((7, CardColor.Green), 2)},
            {"Green 8", ((8, CardColor.Green), 2)},
            {"Green 9", ((9, CardColor.Green), 2)},
            {"Green 0", ((10, CardColor.Green), 1)},
            {"Green Draw Two", ((11, CardColor.Green), 2)},
            {"Green Skip", ((12, CardColor.Green), 2)},
            {"Green Reverse", ((13, CardColor.Green), 2)},

            {"Yellow 1", ((1, CardColor.Yellow), 2)},
            {"Yellow 2", ((2, CardColor.Yellow), 2)},
            {"Yellow 3", ((3, CardColor.Yellow), 2)},
            {"Yellow 4", ((4, CardColor.Yellow), 2)},
            {"Yellow 5", ((5, CardColor.Yellow), 2)},
            {"Yellow 6", ((6, CardColor.Yellow), 2)},
            {"Yellow 7", ((7, CardColor.Yellow), 2)},
            {"Yellow 8", ((8, CardColor.Yellow), 2)},
            {"Yellow 9", ((9, CardColor.Yellow), 2)},
            {"Yellow 0", ((10, CardColor.Yellow), 1)},
            {"Yellow Draw Two", ((11, CardColor.Yellow), 2)},
            {"Yellow Skip", ((12, CardColor.Yellow), 2)},
            {"Yellow Reverse", ((13, CardColor.Yellow), 2)},

            {"Wild", ((14, CardColor.Wild), 4)},
            {"Wild Draw Four", ((15, CardColor.Wild), 4)}
        };
    }
    void Start()
    {
        topCard = transform.Find(discardPileName).GetComponent<Card>();
        for (int i = 0; i < 4; i++)
        {
            players.Add(transform.Find(playerStandsName).GetChild(i).GetComponent<Player>());
        }

        int topCardRandomNumber = UnityEngine.Random.Range(1, 15);

        CardColor topCardRandomColor;
        if (topCardRandomNumber == 14 || topCardRandomNumber == 15) { topCardRandomColor = CardColor.Wild; }
        else
        {
            topCardRandomColor = (CardColor)UnityEngine.Random.Range(0, Enum.GetNames(typeof(CardColor)).Length - 1);
        }

        SetTopCard(topCardRandomNumber, topCardRandomColor);

        UpdateTopCard();

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].name.Split(' ').Last() == "1")
            {
                players[i].GetTurnAndCheckCards();
                break;
            }
        }
    }
    private void UpdateTopCard()
    {
        topCard.UpdateTexture();
    }

    public Card GetTopCard() { return topCard; }

    public void SetTopCard(Card newTopCard)
    {
        topCard.SetNumber(newTopCard.GetNumber());
        topCard.SetColor(newTopCard.GetColor());

        UpdateTopCard();
    }

    public void SetTopCard(int newTopCardNumber, CardColor newTopCardColor)
    {
        topCard.SetNumber(newTopCardNumber);
        topCard.SetColor(newTopCardColor);

        UpdateTopCard();
    }

    public void PlayerFinishedTurn(string playerIndex)
    {
        try
        {
            int nextPlayerIndex = int.Parse(playerIndex);
            nextPlayerIndex = (nextPlayerIndex % 4) + 1; // Increments nextPlayerIndex by one up to 5. When reaches 5, loops back to 1.

            for (int i = 0; i < players.Count; i++)
            {
                if (int.Parse(players[i].name.Split(' ').Last()) == nextPlayerIndex)
                {
                    players[i].GetTurnAndCheckCards();
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public (int, CardColor) DrawCard()
    {
        var card = gameCards.ElementAt(UnityEngine.Random.Range(0, gameCards.Count)).Value;
        while (card.Item2 == 0) { card = gameCards.ElementAt(UnityEngine.Random.Range(0, gameCards.Count)).Value; } // If the card chosen is unavailable, chooses a different one until it finds one that is available
        card.Item2 -= 1;

        return card.Item1;
    }
}
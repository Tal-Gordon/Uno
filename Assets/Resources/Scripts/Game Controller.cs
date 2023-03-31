using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<Player> players = new();

    private GameObject directionArrows;
    [SerializeField] Card topCard;

    public static Dictionary<string, ((int, CardColor), int)> gameCards = new();
    public bool directionClockwise = true;
    // Special rules settings
    public bool stacking = true;
    public bool sevenZero = true;
    public bool jumpIn = true;
    public bool forcePlay = true;
    public bool noBluffing = false;
    public bool drawToMatch = true;

    private readonly string playerStandsName = "Player stands";
    private readonly string discardPileName = "Discard pile";
    private readonly string directionArrowsName = "Direction Arrows";
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

        directionArrows = transform.Find(directionArrowsName).gameObject;
        directionArrows.GetComponent<Animator>().runtimeAnimatorController = Resources.Load("Miscellaneous/Animations/DirectionArrows") as RuntimeAnimatorController;

        int topCardRandomNumber = UnityEngine.Random.Range(1, 15);

        CardColor topCardRandomColor;
        if (topCardRandomNumber == 14 || topCardRandomNumber == 15) { topCardRandomColor = CardColor.Wild; }
        else
        {
            topCardRandomColor = (CardColor)UnityEngine.Random.Range(0, Enum.GetNames(typeof(CardColor)).Length - 1); // We exclude the last color, Wild
        }

        SetTopCard(topCardRandomNumber, topCardRandomColor);

        UpdateTopCard();

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].name.Split(' ').Last() == "1")
            {
                if (topCardRandomNumber == 14)
                {
                    players[i].WildColoring();
                }
                else if (topCardRandomNumber == 15)
                {
                    players[i].Draw4Coloring();
                }
                else
                {
                    players[i].GetTurnAndCheckCards();
                }
                break;
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Your debug function here
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

    public void PlayerFinishedTurn(Player player, Card playedCard)
    {
        try
        {
            int skipModifier = 0;
            bool abortMethod = false;
            if (playedCard != null) 
            { 
                SetTopCard(playedCard);
                if (DoCardAction(playedCard, player.name.Split(' ').Last())) // Return true if the game should skip the turn of the next player, false otherwise
                {
                    skipModifier = directionClockwise ? 1 : -1;
                }
                // The game should stop and await input from the player (who to switch hands with, what color to pick). The player will then call a method to execute his pick, and it in turn will call this method again so the game can continue
                if (playedCard.GetNumber() == 7) 
                {
                    abortMethod = true;
                }
                else if (playedCard.GetNumber() == 14 || playedCard.GetNumber() == 15)
                {
                    if (playedCard.GetColor() == CardColor.Wild)
                    {
                        abortMethod = true;
                    }
                }
            }

            if (!abortMethod)
            {
                int nextPlayerIndex = int.Parse(player.name.Split(' ').Last()) + skipModifier;

                int directionModifier = directionClockwise ? 1 : -1; // Ternary operator. Can be rewritten as: If (directionClockwise) { multiplier = 1; } else { multiplier = -1; }

                nextPlayerIndex = ((nextPlayerIndex - 1 + directionModifier + 4) % 4) + 1; // Calculates the index of the next player based on the current player's index and the direction of play

                for (int i = 0; i < players.Count; i++)
                {
                    if (int.Parse(players[i].name.Split(' ').Last()) == nextPlayerIndex)
                    {
                        players[i].Invoke("GetTurnAndCheckCards", 0.1f);
                        //players[i].GetTurnAndCheckCards();
                        // For now this works, in the final version the server will send each client the permission to play, which won't be instantaneous and won't require Invoke
                        break;
                    }
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

    private bool DoCardAction(Card card, string playerIndex)
    {
        // We return 'false' if the game should continue as normal, or 'true' if we want to skip the next player's turn
        switch (card.GetNumber())
        {
            case 10: // zero
            {
                if (sevenZero)
                {
                    RotateHands();
                }
                return false;
            }
            case 7: // seven
            {
                if (sevenZero)
                {
                    GetPlayerObjectByIndex(playerIndex).ChangeHands();
                }
                return false; 
            }
            case 11: // draw 2
            {
                int directionModifier = directionClockwise ? 1 : -1; 
                int currentPlayerIndex = int.Parse(playerIndex);
                int nextPlayerIndex = ((currentPlayerIndex - 1 + directionModifier + 4) % 4) + 1;

                ForceDrawCards(GetPlayerObjectByIndex(nextPlayerIndex.ToString()), 2);
                if (stacking)
                {
                    // TODO
                }
                return true;
            }
            case 12: // skip
            {
                return true;
            }
            case 13: // reverse
            {
                ChangeDirection();
                return false;
            }
            case 14: // wild
            {
                GetPlayerObjectByIndex(playerIndex).WildColoring();
                return false;
            }
            case 15: // wild draw 4
            {
                if (card.GetColor() == CardColor.Wild) // First part of play
                {
                    GetPlayerObjectByIndex(playerIndex).Draw4Coloring();
                    return false;
                }
                // Second part of play
                int directionModifier = directionClockwise ? 1 : -1;
                int currentPlayerIndex = int.Parse(playerIndex);
                int nextPlayerIndex = ((currentPlayerIndex - 1 + directionModifier + 4) % 4) + 1;

                ForceDrawCards(GetPlayerObjectByIndex(nextPlayerIndex.ToString()), 4);

                if (stacking)
                {
                    // TODO
                }
                return true;
            }
        }
        return false;
    }
    private Player GetPlayerObjectByIndex(string index)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].name.Split(' ').Last() == index) { return players[i]; }
        }
        return null;
    }
    private void ChangeDirection()
    {
        directionClockwise = !directionClockwise;
        directionArrows.GetComponent<SpriteRenderer>().flipX = !directionArrows.GetComponent<SpriteRenderer>().flipX;
        directionArrows.GetComponent<Animator>().SetBool("Clockwise", directionClockwise);
    }
    public void SwapHands(string playerIndex1, string playerIndex2)
    {
        Player firstPlayer = null;
        Player secondPlayer = null;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].name.Split(' ').Last() == playerIndex1)
            {
                firstPlayer = players[i];
            }
            if (players[i].name.Split(' ').Last() == playerIndex2)
            {
                secondPlayer = players[i];
            }
        }
        List<Card> tempDeck = new(firstPlayer.GetDeck()); // We want a clone, not a reference
        firstPlayer.SetDeck(secondPlayer.GetDeck());
        secondPlayer.SetDeck(tempDeck);

        PlayerFinishedTurn(firstPlayer, null);
    }

    private void RotateHands()
    {
        int directionModifier = directionClockwise ? -1 : 1; // It's backwards because it works that way

        // Create a temporary list to store the decks during rotation
        List<List<Card>> tempDecks = new();

        // Copy each player's deck to the temporary list in the rotated order
        for (int i = 0; i < players.Count; i++)
        {
            int index = (i + directionModifier + 4) % players.Count;
            tempDecks.Add(new List<Card>(players[index].GetDeck()));
        }

        // Assign the rotated decks to the players
        for (int i = 0; i < players.Count; i++)
        {
            players[i].SetDeck(tempDecks[i]);
        }
    }

    private void ForceDrawCards(Player player, int amountOfCards)
    {
        for (int i = 0; i < amountOfCards; i++)
        {
            player.DrawCard(); 
        }
    }
}
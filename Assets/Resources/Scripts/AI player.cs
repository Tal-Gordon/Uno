using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public class AIplayer : MonoBehaviour
{
    private Player player;
    private GameController gameController;
    private void Start()
    {
        gameController = GameObject.Find("Board").GetComponent<GameController>();
    }
    public void SetPlayer(Player player) { this.player = player; }
    public string GetActionToDo(string actionName)
    {
        List<Card> deck = new(player.GetDeck());

        switch (actionName.ToLower())
        {
            case "wildcolor":
            {
                // We count the amount of colors in the deck, then return the one that has the most amount of cards
                return GetMostCommonColor(deck).ToString();
            }
            case "draw4color":
            {
                if (gameController.noBluffing)
                {
                    return GetMostCommonColor(deck).ToString();
                }

                CardColor randomColor = (CardColor)UnityEngine.Random.Range(0, Enum.GetNames(typeof(CardColor)).Length - 1); // We exclude the last color, Wild
                return randomColor.ToString();
            }
            case "seven":
            {
                // We choose the player with the least amount of cards
                List<IPlayer> players = new(gameController.players);
                int smallestNumOfCards = 999;
                IPlayer playerToChoose = null;
                for (int i = 0; i < players.Count; i++)
                {
                    if (!players[i].Equals(player))
                    {
                        if (players[i].GetDeck().Count < smallestNumOfCards)
                        {
                            smallestNumOfCards = players[i].GetDeck().Count;
                            playerToChoose = players[i];
                        } 
                    }
                }
                return playerToChoose.GetIndex();
            }
            case "uno":
            {
                if (UnityEngine.Random.value > 0.2f) // A chance of 20% to not call uno, because why not
                {
                    return "uno";
                }
                return null;
            }
            case "keeporplay":
            {
                return "play"; // Ideally we'd keep wilds, but I'm stupid and coded it without access to the card, so...
            }
            case "challenge":
            {
                if (UnityEngine.Random.value >= 0.5f) { return "challenge"; }
                return "decline";
            }
            default:
            {
                return null;
            }
        }
    }
    public void DoPlay()
    {
        List<Card> deck = new(player.GetDeck());
        bool hasPlayableCards = false;
        Debug.Log($"ai player {player.name} is playing");

        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].canPlay) { hasPlayableCards = true; break; }
        }
        if (!hasPlayableCards)
        {
            Debug.Log("drawing cards");
            player.DrawCardsLogic();
        }
        else
        {
            Debug.Log("playing a card");
            Card chosenCard = ChooseCardFromDeck(deck);
            if (chosenCard != null)
            {
                gameController.PlayerFinishedTurn(player, chosenCard); 
            }
            else
            {
                Debug.Log("actually, drawing cards");
                player.DrawCardsLogic();
            }
        }
    }
    private Card ChooseCardFromDeck(List<Card> deck)
    {
        Card matchingCard = deck.FirstOrDefault(deckCard => player.GetCardPlayable(deckCard)); // first element in the list that matches the specified condition, null if no such element is found
        if (matchingCard != null)
        {
            return matchingCard;
        }
        return null;
    }
    private CardColor GetMostCommonColor(List<Card> deck)
    {
        var colorCounts = new Dictionary<CardColor, int>();

        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i].GetColor() != CardColor.Wild)
            {
                if (colorCounts.ContainsKey(deck[i].GetColor()))
                {
                    colorCounts[deck[i].GetColor()]++;
                }
                else
                {
                    colorCounts[deck[i].GetColor()] = 1;
                }
            }
        }

        return colorCounts.OrderByDescending(x => x.Value).First().Key;
    }
}

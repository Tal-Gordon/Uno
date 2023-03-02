using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<Player> players = new();
    [SerializeField] Card topCard;
    
    private List<Card> cards = new();

    private readonly string playerStandsName = "Player stands";
    private readonly string discardPileName = "Discard pile";
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

    public Card GetTopCard()
    {
        return topCard;
    }

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
            nextPlayerIndex = (nextPlayerIndex + 1) % players.Count;

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
            Debug.Log($"Exception: \n{e}");
        }
    }
}
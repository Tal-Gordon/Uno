using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public List<IPlayer> players = new();
    public Player selfPlayer;

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
    public int stacked = 0;

    private Server server;
    private Client client;

    private CardColor challengedColor;
    private bool[] unoedPlayers = new bool[4];
    private void Awake()
    {
        server = Server.Instance; client = Client.Instance;
        if (true || server.active) // Remove later
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
    }
    void Start()
    {
        topCard = transform.Find("Discard pile").GetComponent<Card>();

        for (int i = 0; i < 4; i++)
        {
            players.Add(transform.Find("Player stands").GetChild(i).GetComponent<IPlayer>());
        }
        selfPlayer = transform.Find("Player stands").GetChild(0).GetComponent<Player>();

        directionArrows = transform.Find("Direction Arrows").gameObject;
        directionArrows.GetComponent<Animator>().runtimeAnimatorController = Resources.Load("Miscellaneous/Animations/DirectionArrows") as RuntimeAnimatorController;

        StartCoroutine(LateStart(0.5f)); // Remove later
    }
    public IEnumerator LateStart(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);

        for (int i = 0; i < unoedPlayers.Length; i++)
        {
            unoedPlayers[i] = false;
        }

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
            players[i].StartNewGame(); 
        }
        //var firstPlayer = players.FirstOrDefault(player => player.GetIndex() == "1");
        //if (firstPlayer != null)
        //{
        //    firstPlayer.FinishTurn(GetTopCard());
        //}
        GetIPlayerByIndex("1").FinishTurn(GetTopCard());
        //for (int i = 0; i < players.Count; i++)
        //{
        //    if (players[i] is Player)
        //    {
        //        Player player = (Player)players[i];
        //        if (player.GetIndex() == "1")
        //        {
        //            player.FinishTurn(GetTopCard());
        //        }
        //    }
        //    else
        //    {
        //        FakePlayer player = (FakePlayer)players[i];
        //        if (player.GetIndex() == "1")
        //        {
        //            player.FinishTurn(GetTopCard());
        //        }
        //    }
        //}
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

    public void IndexPlayers(string selfIndex)
    {
        string index = selfIndex;
        for (int i = 0; i < 4; i++)
        {
            transform.Find("Player stands").GetChild(i).gameObject.name = $"Player {index}";
            index = int.Parse(GetNextPlayerIndex(index)).ToString(); // Direction at start of game is clockwise
        }
    }

    public void PlayerFinishedTurn(IPlayer player, Card playedCard)
    {
        try
        {
            player.FinishTurn(playedCard); // Only removes card from deck
            int skipModifier = 0;
            bool abortMethod = false; // Should only be true if awaiting additional input from player
            bool playerYetToUno = false;

            if (playedCard != null)
            {
                if (GetPlayerNumOfCardsByIndex(player.GetIndex()) != 0)
                {
                    if (!noBluffing && playedCard.GetNumber() == 15) { challengedColor = playedCard.GetColor(); }
                    SetTopCard(playedCard);
                    if (DoCardAction(playedCard, player.GetIndex())) // Return true if the game should skip the turn of the next player, false otherwise
                    {
                        skipModifier = directionClockwise ? 1 : -1;
                    }
                    //  Additional input is required from the player, or from the next. Game should wait before giving next player turn, instead when finished player will call the method again with a null card
                    if (playedCard.GetNumber() == 7 || (stacking && (playedCard.GetNumber() is 11 or 15)))
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
                    if (GetPlayerNumOfCardsByIndex(player.GetIndex()) == 1 && !unoedPlayers[int.Parse(player.GetIndex())-1])
                    {
                        playerYetToUno = true;
                        if (!player.Equals(gameObject))
                        {
                            players.OfType<Player>().First().ShowCallOutButton();
                        }
                    } 
                }
                else
                {
                    if (server.active) { selfPlayer.ShowMatchEndPanelServer(); }
                    else { selfPlayer.ShowMatchEndPanelClient(); }
                }
            }

            if (!playerYetToUno)
            {
                selfPlayer.UnshowCallOutButton();
            }

            if (jumpIn)
            {
                //for (int i = 0; i < players.Count; i++)
                //{
                //    players[i].CheckForJumpIn();
                //}
                //yield return new WaitForSeconds(1.5f);
            }

            if (!abortMethod)
            {
                int nextPlayerIndex = int.Parse(GetNextPlayerIndex((int.Parse(player.GetIndex()) + skipModifier).ToString()));

                if (nextPlayerIndex.ToString() == selfPlayer.GetIndex()) { Invoke(nameof(selfPlayer.GetTurnAndCheckCards), 1f); }
                //for (int i = 0; i < players.Count; i++)
                //{
                //    if (int.Parse(players[i].GetIndex()) == nextPlayerIndex)
                //    {
                //        players[i].Invoke("GetTurnAndCheckCards", 1f);
                //        //players[i].GetTurnAndCheckCards();
                //        // For now this works, in the final version the server will send each client the permission to play, which won't be instantaneous and won't require Invoke
                //        break;
                //    }
                //} 
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    public void PlayerFinishedTurn(string playerIndex, Card playedCard)
    {
        PlayerFinishedTurn(GetIPlayerByIndex(playerIndex), playedCard);
    }

    public void CalledOutUnunoed(string callingPlayerIndex) 
    {
        if (server.active)
        {
            server.CalledOutUnunoed(callingPlayerIndex);
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
                    GetIPlayerByIndex(playerIndex).ChangeHands();
                }
                return false; 
            }
            case 11: // draw 2
            {
                //if (stacking)
                //{
                //    StackCard(playerIndex, card);
                //}
                //else
                //{
                //    string nextPlayerIndex = GetNextPlayerIndex(playerIndex);
                //    ForceDrawCards(GetIPlayerByIndex(nextPlayerIndex), 2);
                //}
                // TODO
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
                if (card.GetColor() == CardColor.Wild) // First part of play
                {
                    selfPlayer.WildColoring();
                }
                // Second part of play
                return false;
            }
            case 15: // wild draw 4
            {
                if (card.GetColor() == CardColor.Wild) // First part of play
                {
                    selfPlayer.Draw4Coloring();
                    return false;
                }
                // Second part of play
                
                //if (stacking)
                //{
                //    StackCard(playerIndex, card);
                //}
                //else
                //{
                //    if (noBluffing)
                //    {
                //        string nextPlayerIndex = GetNextPlayerIndex(playerIndex);
                //        ForceDrawCards(GetIPlayerByIndex(nextPlayerIndex), 4); 
                //    }
                //}
                // TODO
                return true;
            }
        }
        return false;
    }
    private IPlayer GetIPlayerByIndex(string index)
    {
        //bool selfPlayerBool = false;
        //if (index == selfPlayer.GetIndex()) { selfPlayerBool = true; }
        for (int i = 0; i < players.Count; i++)
        {
            //if (selfPlayerBool && players[i] is Player) { return (Player)players[i]; }
            //else if (!selfPlayerBool && players[i] is FakePlayer) { return (FakePlayer)players[i]; }
            if (players[i].GetIndex() == index) { return players[i]; }
        }
        return null;
    }
    public GameObject GetPlayerObjectByIndex(string index)
    {
        Transform players = transform.GetChild(0);
        for (int i = 0; i < 4; i++)
        {
            if (players.GetChild(i).name.Split(' ').Last() == index) { return players.GetChild(i).gameObject; }
        }
        return null;
    }
    public int GetPlayerNumOfCardsByIndex(string index)
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject player = transform.Find("Player stands").GetChild(i).gameObject;
            if (player.name.Split(' ').Last() == index)
            {
                return player.transform.GetChild(0).GetComponent<HandLayout>().cards.Count;
            }
        }
        return -1;
    }
    private void ChangeDirection()
    {
        directionClockwise = !directionClockwise;
        directionArrows.GetComponent<SpriteRenderer>().flipX = !directionArrows.GetComponent<SpriteRenderer>().flipX;
        directionArrows.GetComponent<Animator>().SetBool("Clockwise", directionClockwise);
    }
    public void SwapHands(string playerIndex1, string playerIndex2) // Method for swapping two fake players
    {
        IPlayer firstPlayer = GetIPlayerByIndex(playerIndex1);
        IPlayer secondPlayer = GetIPlayerByIndex(playerIndex2);

        List<Card> tempDeck = new(firstPlayer.GetDeck()); // We want a clone, not a reference
        firstPlayer.SetDeck(secondPlayer.GetDeck());
        secondPlayer.SetDeck(tempDeck);
    }
    public void SwapHands(string chosenPlayerIndex, List<Card> newDeck) // Method for swapping self deck with other deck
    {
        if (server.active) 
        { 
            return; 
        }
        else if (client.active) 
        { 
            return; 
        }
        IPlayer otherPlayer = GetIPlayerByIndex(chosenPlayerIndex);
        otherPlayer.SetDeck(new(selfPlayer.GetDeck()));
        selfPlayer.SetDeck(newDeck);

        //PlayerFinishedTurn(selfPlayer, null);
    }

    private void RotateHands() // The method assigns the decks to a new list of lists in a rotated way, then reassigns them to the players
    {
        int directionModifier = directionClockwise ? -1 : 1; // It's backwards because otherwise it doesn't work

        List<List<Card>> tempDecks = new();

        for (int i = 0; i < players.Count; i++)
        {
            int index = (i + directionModifier + 4) % players.Count;
            tempDecks.Add(new List<Card>(players[index].GetDeck()));
        }

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
    public void StackCard(string playerIndex, Card card)
    {
        //string nextPlayerIndex = GetNextPlayerIndex(playerIndex);

        //Player nextPlayer = GetIPlayerByIndex(nextPlayerIndex);
        //List<Card> playerDeck = nextPlayer.GetDeck();
        //bool hasCardToStack = false;
        //for (int i = 0; i < playerDeck.Count; i++)
        //{
        //    if (playerDeck[i].GetNumber() == card.GetNumber())
        //    {
        //        hasCardToStack = true;
        //        break;
        //    }
        //}

        //stacked += 2;
        //if (card.GetNumber() == 15) { stacked += 2; }

        //if (hasCardToStack)
        //{
        //    nextPlayer.GetTurnAndCheckForStacking(card);
        //}
        //else
        //{
        //    ForceDrawCards(nextPlayer, stacked);
        //    stacked = 0;
        //    PlayerFinishedTurn(nextPlayer, null);
        //}
        // TODO
    }
    private string GetNextPlayerIndex(string playerIndex)
    {
        int directionModifier = directionClockwise ? 1 : -1; // Ternary operator. Can be rewritten as: If (directionClockwise) { multiplier = 1; } else { multiplier = -1; }
        return (((int.Parse(playerIndex) - 1 + directionModifier + 4) % 4) + 1).ToString(); // Calculates the index of the next player based on the current player's index and the direction of play
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum CardColor { Red, Green, Yellow, Blue, Wild };
public class Card : MonoBehaviour
{
    /*
        1-10 are regular (1, 2, 3... 0)
        11 is draw 2
        12 is skip
        13 is reverse
        14 is wild
        15 is wild draw 4
    */

    [SerializeField] int number;
    [SerializeField] CardColor color;

    public bool canPlay;
    public bool handCard; // Some objects in my scene have this script but aren't meant to be played. This bool is meant to mark the cards in the players' hands, those that are playable
    public bool hidden;

    private SpriteRenderer cardSprite;
    private BoxCollider2D boxCollider;
    private static Sprite[] cardSpriteSheet;

    private bool mouseEnteredCard = false;
    void Awake()
    {
        cardSpriteSheet = Resources.LoadAll<Sprite>("Graphics/Cards");
        boxCollider = GetComponent<BoxCollider2D>();
        cardSprite = GetComponent<SpriteRenderer>();
    }
    void Start()
    {
        boxCollider.size = new Vector2(cardSprite.size.x, cardSprite.size.y);
        boxCollider.offset = new Vector2(cardSprite.size.x/2, 0);

        UpdateTexture();

        /*
            Texture convention - corresponds to cards sprite sheet

            1 - wild
            6 - wild draw 4
            12-24 - yellows - numbers; draw 2; skip; reverse
            25-37 - reds - numbers; draw 2; skip; reverse
            38-50 - blues - numbers; draw 2; skip; reverse
            51-63 - greens - numbers; draw 2; skip; reverse
         */
    }

    public int GetNumber() { return number; }
    public CardColor GetColor() { return color; }
    public void SetNumber(int newNumber)
    {
        number = newNumber;
    }
    public void SetColor(CardColor newColor)
    {
        color = newColor;
    }
    //public void MutateColor(CardColor newColor)
    //{ 
    //    //mutator that changes the color of a wild card to make the color noticeable
    //    SetColor(newColor);
    //    if (GetNumber() == 14) // Regular wild
    //    {
    //        switch (newColor)
    //        {
    //            case CardColor.Yellow:
    //            { cardSprite.sprite = cardSpriteSheet[2]; break; }
    //            case CardColor.Red:
    //            { cardSprite.sprite = cardSpriteSheet[3]; break; }
    //            case CardColor.Blue:
    //            { cardSprite.sprite = cardSpriteSheet[4]; break; }
    //            case CardColor.Green:
    //            { cardSprite.sprite = cardSpriteSheet[5]; break; }
    //        }
    //    }
    //    else if (GetNumber() == 15) // Wild Draw 4
    //    {
    //        switch (newColor)
    //        {
    //            case CardColor.Yellow:
    //            { cardSprite.sprite = cardSpriteSheet[7]; break; }
    //            case CardColor.Red:
    //            { cardSprite.sprite = cardSpriteSheet[8]; break; }
    //            case CardColor.Blue:
    //            { cardSprite.sprite = cardSpriteSheet[9]; break; }
    //            case CardColor.Green:
    //            { cardSprite.sprite = cardSpriteSheet[10]; break; }
    //        }
    //    }
    //    else
    //    {
    //        Debug.Log("Error: card isn't wild, cannot change color");
    //        Debug.Log($"Received card with number {GetNumber()}");
    //    }
    //}
    private int GetCorrectMutateColorSpriteSheetNumber(CardColor color) // This function exists because I hate myself (and how I wrote other functions)
    {
        if (GetNumber() == 14)
        {
            switch (color)
            {
                case CardColor.Yellow:
                {
                    return 2;
                }
                case CardColor.Red:
                {
                    return 3;
                }
                case CardColor.Blue:
                {
                    return 4;
                }
                case CardColor.Green:
                {
                    return 5;
                }
            }
        }
        else if (GetNumber() == 15)
        {
            switch (color)
            {
                case CardColor.Yellow:
                {
                    return 7;
                }
                case CardColor.Red:
                {
                    return 8;
                }
                case CardColor.Blue:
                {
                    return 9;
                }
                case CardColor.Green:
                {
                    return 10;
                }
            }
        }
        return -999;
    }

    public void UpdateTexture()
    {
        int spritePathNum = 0;
        switch (color)
        {
            case CardColor.Red:
            { 
                spritePathNum = 24 + number; 
                if (GetNumber() == 14 || GetNumber() == 15) { spritePathNum = GetCorrectMutateColorSpriteSheetNumber(color); }
                break; 
            }
            case CardColor.Green:
            { 
                spritePathNum = 50 + number;
                if (GetNumber() == 14 || GetNumber() == 15) { spritePathNum = GetCorrectMutateColorSpriteSheetNumber(color); }
                break; 
            }
            case CardColor.Yellow:
            { 
                spritePathNum = 11 + number;
                if (GetNumber() == 14 || GetNumber() == 15) { spritePathNum = GetCorrectMutateColorSpriteSheetNumber(color); }
                break; 
            }
            case CardColor.Blue:
            { 
                spritePathNum = 37 + number;
                if (GetNumber() == 14 || GetNumber() == 15) { spritePathNum = GetCorrectMutateColorSpriteSheetNumber(color); }
                break; 
            }
            case CardColor.Wild:
            {
                switch (number)
                {
                    case 14:
                    { spritePathNum = 1; break; }
                    case 15:
                    { spritePathNum = 6; break; }
                    default:
                    { spritePathNum = 0; break; }
                }
                break;
            }
        }
        cardSprite.sprite = cardSpriteSheet[spritePathNum];
        if (hidden) { cardSprite.sprite = cardSpriteSheet[0]; }
    }

    private void OnMouseEnter()
    {
        if (handCard && transform.parent.parent.GetComponent<Player>().GetCanPlay())
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y + 0.25f, transform.localPosition.z);
            mouseEnteredCard = true;
        }
    }
    private void OnMouseOver()
    {
        if (handCard && transform.parent.parent.GetComponent<Player>().GetCanPlay() && !mouseEnteredCard) // Fix for when player gets turn with mouse on card
        {
            OnMouseEnter();
        }
    }
    private void OnMouseExit()
    {
        if (handCard && transform.parent.parent.GetComponent<Player>().GetCanPlay() && mouseEnteredCard)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y - 0.25f, transform.localPosition.z);
        }
        mouseEnteredCard = false;
    }
    public void DestroyCard() { Destroy(gameObject); }
}
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using BudovaBIM.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace BudovaBIM.ArchitecturePanel.RoomManager
{
    [Transaction(TransactionMode.Manual)]

    internal class RoomManagerCommand : IExternalCommand
    {
        Document document;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            document = commandData.Application.ActiveUIDocument.Document;
            UIApplication application = commandData.Application;

            List<Room> rooms = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .ToList();

            if (rooms.Count == 0)
            {
                MessageBox.Show("Помещения не найдены!");
                return Result.Failed;
            }

            if (rooms.First().LookupParameter(SharedParameters.ApartmentNumber) == null)
            {
                MessageBox.Show($"Не найден общий параметр - {SharedParameters.ApartmentNumber}!");
                return Result.Failed;
            }

            List<MyLotOfSale> lotsOfSales = rooms
                .GroupBy(room => room.LookupParameter(SharedParameters.ApartmentNumber).AsString())
                .ToDictionary(keySelector: it => it.Key, elementSelector: it => it.ToList())
                .Select(KeyValuePair => new MyLotOfSale(KeyValuePair.Key, KeyValuePair.Value)).ToList();

            int progressBarMaxValue = lotsOfSales.Count;

            using (var progressBarView = new ProgressBar(application, "Work in progress..", progressBarMaxValue))
            {
                progressBarView.Show();

                using (Transaction tr = new Transaction(document, "Rooms"))
                {
                    tr.Start();

                    foreach (MyLotOfSale lot in lotsOfSales)
                    {
                        lot.SetParameters();

                        progressBarView.Update();
                        if (progressBarView.IsCanceled)
                        {
                            break;
                        }
                    }

                    var apartments = lotsOfSales.Where(it => it.Number.StartsWith("A") == true).ToList();
                    SetTypingApartment(apartments);

                    var otherLots = lotsOfSales.Where(it => it.Number.StartsWith("A") == false).ToList();
                    foreach (var lot in otherLots)
                    {
                        foreach (var room in lot.Rooms)
                        {
                            room.RevitElement.LookupParameter(SharedParameters.ApartmentType).Set(lot.Number);
                        }
                    }


                    if (progressBarView.IsCanceled)
                    {
                        tr.RollBack();
                    }
                    else
                    {
                        tr.Commit();
                    }
                }
            }

            return Result.Succeeded;
        }

        private void SetTypingApartment(List<MyLotOfSale> apartments) // Назначение типа 1А, 1Б, 2А и т.п.
        {
            // Символы для назначения типов
            string charsForTyping = "ABCDEFGHJKLMNPQRSTUWXY";

            Dictionary<string, List<MyLotOfSale>> groupByLevel = apartments
                .GroupBy(apart => apart.LevelName)
                .ToDictionary(keySelector: it => it.Key, elementSelector: it => it.ToList());

            Dictionary<string, Dictionary<int, List<MyLotOfSale>>> groupByLivingRoomCount = new Dictionary<string, Dictionary<int, List<MyLotOfSale>>>();
            foreach (var keyValuePair in groupByLevel)
            {
                groupByLivingRoomCount[keyValuePair.Key] = keyValuePair.Value
                    .GroupBy(apart => apart.NumberOfLivingRooms)
                    .ToDictionary(keySelector: apart => apart.Key, elementSelector: apart => apart.ToList());
            }

            foreach (var keyValuePair in groupByLivingRoomCount)
            {
                string levelName = keyValuePair.Key;
                Dictionary<int, List<MyLotOfSale>> groupApartByLivingCountRoom = keyValuePair.Value;

                foreach (var keyValuePair2 in groupApartByLivingCountRoom)
                {
                    int countLivingRoom = keyValuePair2.Key;
                    var groupApartments = keyValuePair2.Value.OrderBy(it => int.Parse(Regex.Replace(it.Number, "[^0-9.]", ""))).ToList();

                    foreach (MyLotOfSale myApartment in groupApartments)
                    {
                        foreach (MyRoom room in myApartment.Rooms)
                        {
                            string typeApart = $"{countLivingRoom}{charsForTyping[groupApartments.IndexOf(myApartment)]}";
                            room.RevitElement.LookupParameter(SharedParameters.ApartmentType).Set(typeApart);
                        }
                    }
                }
            }
        }
    }

    class MyLotOfSale
    {
        public string Number { get; set; }
        public List<MyRoom> Rooms { get; set; }
        public int NumberOfLivingRooms // Количество жилых комнат
        {
            get
            {
                return Rooms.Where(room => room.RoomType == RoomTypeEnum.LivingRoom).Count();
            }
        }
        public double Area // Площадь без балконов и лоджий
        {
            get
            {
                return Rooms.Where(room => room.IsLoggiaOrBalkony == false).Select(room => room.AreaWithFactor).Sum();
            }
        }
        public double LivingRoomArea // Площадь жилая
        {
            get
            {
                return Rooms.Where(room => room.RoomType == RoomTypeEnum.LivingRoom).Select(room => room.AreaWithFactor).Sum();
            }
        }

        public double LivingRoomSalesArea // Площадь жилая
        {
            get
            {
                return Rooms.Where(room => room.RoomType == RoomTypeEnum.LivingRoom).Select(room => room.AreaSalesWithFactor).Sum();
            }
        }
        public double GeneralArea // Общая площадь
        {
            get
            {
                return Rooms.Select(room => room.AreaWithFactor).Sum();
            }
        }
        public double GeneralSalesArea // Общая площадь
        {
            get
            {
                return Rooms.Select(room => room.AreaSalesWithFactor).Sum();
            }
        }

        public string LevelName //Имя этажа
        {
            get
            {
                return Rooms.First().RevitElement.Level.Name;
            }
        }
        public virtual string Department
        {
            get
            {
                if (Number.StartsWith("A")) return "Квартири";
                else if (Number.StartsWith("C") || Number.StartsWith("С")) return "Комерційні приміщення";
                else if (Number.StartsWith("S")) return "Нежитлові приміщення";
                else if (Number.StartsWith("T")) return "Технічні приміщення";
                else if (Number.StartsWith("P")) return "Паркінг";
                else if (Number.StartsWith("G")) return "Місця загального користування";
                else return "Не удалось определить!";
            }
        }
        public virtual string Occupancy
        {
            get
            {
                if (Number.StartsWith("A")) return $"{NumberOfLivingRooms}-кімнатні квартири";
                else if (Number.StartsWith("C") || Number.StartsWith("С")) return "Комерційні приміщення";
                else if (Number.StartsWith("S")) return "Нежитлові приміщення";
                else if (Number.StartsWith("T")) return "Технічні приміщення";
                else if (Number.StartsWith("P")) return "Паркінг";
                else if (Number.StartsWith("G")) return "Місця загального користування";
                else return "Не удалось определить!";
            }
        }

        public MyLotOfSale(string number, List<Room> rooms)
        {
            Number = number;
            Rooms = rooms.Select(room => new MyRoom(room)).ToList();
        }

        public void SetParameters()
        {
            SetLivingRoomCount();
            SetAreaFactor();
            SetAreaWithFactor();
            SetArea();
            SetLivingArea();
            SetGeneralArea();
            SetRoomIndex();
            SetDepartment();
            SetOccupancy();
        }

        private void SetLivingRoomCount()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                myRoom.RevitElement.LookupParameter(SharedParameters.LivingRoomCountParameter).Set(NumberOfLivingRooms);
            }
        }
        private void SetAreaFactor()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                myRoom.RevitElement.LookupParameter(SharedParameters.AreaFactorParameter).Set(myRoom.AreaFactor);
            }
        }
        private void SetAreaWithFactor()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                double area = UnitUtils.ConvertToInternalUnits(myRoom.AreaWithFactor, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.AreaWithFactorParameter).Set(area);

                double areaSales = UnitUtils.ConvertToInternalUnits(myRoom.AreaSalesWithFactor, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.RoomAreaParameter).Set(areaSales);
            }
        }
        private void SetArea()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                double area = UnitUtils.ConvertToInternalUnits(Area, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.ApartmentAreaParameter).Set(area);
            }
        }
        private void SetLivingArea()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                double area = UnitUtils.ConvertToInternalUnits(LivingRoomArea, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.ApartmentLivingAreaParameter).Set(area);


                double areaSales = UnitUtils.ConvertToInternalUnits(LivingRoomSalesArea, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.ApartmentLivingAreaParameter2).Set(area);
            }
        }
        private void SetGeneralArea()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                double area = UnitUtils.ConvertToInternalUnits(GeneralArea, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.ApartmentGeneralAreaParameter).Set(area);

                double areaSales = UnitUtils.ConvertToInternalUnits(GeneralSalesArea, UnitTypeId.SquareMeters);
                myRoom.RevitElement.LookupParameter(SharedParameters.ApartmentGeneralAreaParameter2).Set(areaSales);
            }
        }
        private void SetRoomIndex()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                string roomIndex = $"{Number}_{(int)myRoom.RoomType}";
                myRoom.RevitElement.LookupParameter(SharedParameters.RoomIndexParameter).Set(roomIndex);
            }
        }
        private void SetDepartment()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                myRoom.RevitElement.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT).Set(Department);
            }
        }
        private void SetOccupancy()
        {
            foreach (MyRoom myRoom in Rooms)
            {
                myRoom.RevitElement.get_Parameter(BuiltInParameter.ROOM_OCCUPANCY).Set(Occupancy);
            }
        }
    }

    class MyRoom
    {
        public Room RevitElement { get; }
        public RoomTypeEnum RoomType // Тип помещения от пользователя
        {
            get
            {
                return (RoomTypeEnum)RevitElement.LookupParameter(SharedParameters.RoomTypeParameter).AsInteger();
            }
        }
        public bool IsLoggiaOrBalkony // Помещение это балкон или лоджия 
        {
            get
            {
                return new List<RoomTypeEnum>() {
                    RoomTypeEnum.Loggia,
                    RoomTypeEnum.GlazedLogia,
                    RoomTypeEnum.Balkony,
                    RoomTypeEnum.GlazedBalkony }.Contains(RoomType);
            }
        }

        public double Area // Системная площадь, округленная до двух знаков после запятой
        {
            get
            {
                return Math.Round(UnitUtils.ConvertFromInternalUnits(RevitElement.Area, UnitTypeId.SquareMeters) * 100, 2, MidpointRounding.AwayFromZero) / 100;
            }
        }
        public double AreaSales // Системная площадь, округленная до одного знака после запятой
        {
            get
            {
                return Math.Round(UnitUtils.ConvertFromInternalUnits(RevitElement.Area, UnitTypeId.SquareMeters), 1, MidpointRounding.AwayFromZero);
            }
        }

        public double AreaFactor // Коэффициент площади
        {
            get
            {
                switch (RoomType)
                {
                    case RoomTypeEnum.LivingRoom:
                        return 1;
                    case RoomTypeEnum.NotLivingRoom:
                        return 1;
                    case RoomTypeEnum.Loggia:
                        return 0.5;
                    case RoomTypeEnum.GlazedLogia:
                        return 1;
                    case RoomTypeEnum.Balkony:
                        return 0.3;
                    case RoomTypeEnum.GlazedBalkony:
                        return 1;
                    case RoomTypeEnum.PublicRoom:
                        return 1;
                    default:
                        return 0;
                }
            }
        }

        public double AreaWithFactor // Площадь с коэффициентом
        {
            get
            {
                return Math.Round(Area * AreaFactor, 2, MidpointRounding.AwayFromZero);
            }
        }
        public double AreaSalesWithFactor // Площадь с коэффициентом
        {
            get
            {
                return Math.Round(AreaSales * AreaFactor, 1, MidpointRounding.AwayFromZero);
            }
        }

        public string LevelName //Имя этажа
        {
            get
            {
                return RevitElement.Level.Name;
            }
        }

        public MyRoom(Room room)
        {
            RevitElement = room;
        }
    }

    enum RoomTypeEnum
    {
        LivingRoom = 1,
        NotLivingRoom = 2,
        Loggia = 3,
        GlazedLogia = 4,
        Balkony = 5,
        GlazedBalkony = 6,
        PublicRoom = 7,
    }
}

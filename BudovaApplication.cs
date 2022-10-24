using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BudovaBIM.ArchitecturePanel.RoomManager;
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace BudovaBIM
{
    internal class BudovaApplication : IExternalApplication
    {
        public static string AppTitle = "БУДОВА";
        public static string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        public static string iconsDirectoryPath = Path.GetDirectoryName(assemblyLocation) + @"\icons\";

        public Result OnStartup(UIControlledApplication application)
        {

            application.CreateRibbonTab(AppTitle);

            #region Document Panel
            RibbonPanel DocumentRibbonPanel = application.CreateRibbonPanel(AppTitle, "Документ");
            #endregion

            #region General Panel
            RibbonPanel GeneralRibbonPanel = application.CreateRibbonPanel(AppTitle, "Обшие");
            #endregion

            #region Hole Panel
            RibbonPanel HoleRibbonPanel = application.CreateRibbonPanel(AppTitle, "Отверстия");
            #endregion

            #region Architecture Panel
            RibbonPanel ArchitectureRibbonPanel = application.CreateRibbonPanel(AppTitle, "AR00");

            CreateButton(typeof(RoomManagerCommand), "Квартиро-\nграфия", "apartment.png", ArchitectureRibbonPanel);
            #endregion

            #region MEP Panel
            RibbonPanel MEPRibbonPanel = application.CreateRibbonPanel(AppTitle, "MEP0");
            #endregion

            #region Structural building (SB00) Panel
            RibbonPanel StructuralBuildingRibbonPanel = application.CreateRibbonPanel(AppTitle, "SB00");
            #endregion

            #region Structural Concrete Fiber (SCF0) Panel
            RibbonPanel StructuralConcreteFiberRibbonPanel = application.CreateRibbonPanel(AppTitle, "SCF0");
            #endregion

            #region Structural Concrete (SC00) Panel
            RibbonPanel StructuralConcreteRibbonPanel = application.CreateRibbonPanel(AppTitle, "SC00");
            #endregion

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public class IsFamilyDocument : IExternalCommandAvailability
        {
            public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            {
                try
                {
                    if (applicationData.ActiveUIDocument.Document.IsFamilyDocument)
                    {
                        return true;
                    }
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private void CreateButton(Type command, string commandName, string iconName, RibbonPanel panel, bool onlyFamilyDocument = false)
        {
            PushButtonData pushButtonData = new PushButtonData(commandName, commandName, assemblyLocation, command.FullName);
            pushButtonData.LargeImage = new BitmapImage(new Uri(iconsDirectoryPath + iconName));
            var pushButton = panel.AddItem(pushButtonData) as PushButton;

            if (onlyFamilyDocument == true)
            {
                pushButton.AvailabilityClassName = typeof(IsFamilyDocument).FullName;
            }
        }
        private PushButtonData CreatePushButtonData(Type command, string commandName, string iconName, bool smalImage = false)
        {
            PushButtonData pushButtonData = new PushButtonData(commandName, commandName, assemblyLocation, command.FullName);
            if (smalImage)
            {
                pushButtonData.Image = new BitmapImage(new Uri(iconsDirectoryPath + iconName));
            }
            else
            {
                pushButtonData.LargeImage = new BitmapImage(new Uri(iconsDirectoryPath + iconName));
            }
            return pushButtonData;
        }
    }
}

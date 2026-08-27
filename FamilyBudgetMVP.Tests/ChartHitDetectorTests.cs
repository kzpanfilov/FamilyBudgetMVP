using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class ChartHitDetectorTests
    {
        [Fact]
        public void HitTest_Returns_Null_When_No_Zones()
        {
            var detector = new ChartHitDetector();

            Assert.Null(detector.HitTest(100, 100));
        }

        [Fact]
        public void HitTest_Returns_Category_When_Inside_Zone()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 100, 200, "Продукты");

            Assert.Equal("Продукты", detector.HitTest(50, 100));
        }

        [Fact]
        public void HitTest_Returns_Null_When_Outside()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 100, 200, "Продукты");

            Assert.Null(detector.HitTest(150, 100));
        }

        [Fact]
        public void HitTest_Returns_Null_On_Boundary_Exactly()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(10, 10, 50, 100, "A");

            Assert.Equal("A", detector.HitTest(10, 10));
            Assert.Equal("A", detector.HitTest(50, 100));
            Assert.Null(detector.HitTest(9, 10));
            Assert.Null(detector.HitTest(10, 9));
        }

        [Fact]
        public void HitTest_First_Matching_Zone_Wins()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 200, 200, "A");
            detector.AddZone(100, 0, 200, 200, "B");

            Assert.Equal("A", detector.HitTest(150, 100));
        }

        [Fact]
        public void HitTest_Different_Categories_In_Separate_Zones()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 50, 200, "Продукты");
            detector.AddZone(50, 0, 100, 200, "Транспорт");
            detector.AddZone(100, 0, 150, 200, "Жильё");

            Assert.Equal("Продукты", detector.HitTest(25, 100));
            Assert.Equal("Транспорт", detector.HitTest(75, 100));
            Assert.Equal("Жильё", detector.HitTest(125, 100));
            Assert.Null(detector.HitTest(175, 100));
        }

        [Fact]
        public void Clear_Removes_All_Zones()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 100, 200, "A");

            detector.Clear();

            Assert.Null(detector.HitTest(50, 100));
            Assert.Equal(0, detector.Count);
        }

        [Fact]
        public void AddZone_Multiple_Zones_Increases_Count()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(0, 0, 10, 10, "A");
            detector.AddZone(20, 0, 30, 10, "B");

            Assert.Equal(2, detector.Count);
        }

        [Fact]
        public void HitTest_Top_Left_And_Bottom_Right_Corners()
        {
            var detector = new ChartHitDetector();
            detector.AddZone(10, 20, 100, 200, "X");

            Assert.Equal("X", detector.HitTest(10, 20));
            Assert.Equal("X", detector.HitTest(100, 200));
            Assert.Null(detector.HitTest(9, 20));
            Assert.Null(detector.HitTest(100, 201));
        }

        [Fact]
        public void Full_Width_Simulated_Bar_Slots()
        {
            var detector = new ChartHitDetector();

            float barWidth = 80;
            float chartWidth = 300;
            float slotWidth = chartWidth / 3;

            for (int i = 0; i < 3; i++)
            {
                float left = i * slotWidth;
                float right = left + slotWidth;
                detector.AddZone(left, 0, right, 300, $"Cat{i}");
            }

            Assert.Equal("Cat0", detector.HitTest(40, 150));
            Assert.Equal("Cat1", detector.HitTest(140, 150));
            Assert.Equal("Cat2", detector.HitTest(240, 150));
            Assert.Null(detector.HitTest(310, 150));
        }
    }
}

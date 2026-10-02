using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
// WinForms 와 Revit API 에 같은 이름이 있는 클래스는 어느 쪽을 쓸지 지정합니다.
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace Modless
{
    // ═══════════════════════════════════════════════════════════════
    //  ExternalEvent 란?
    // ═══════════════════════════════════════════════════════════════
    //
    //  ■ 왜 필요한가?
    //    Revit 은 "지금은 애드인 차례" 라고 허락한 동안에만 Revit API 를 쓸 수 있게 합니다.
    //    (이 "애드인 차례" 를 API 컨텍스트 라고 부릅니다.)
    //    명령(Command)의 Execute() 가 실행되는 동안이 바로 "애드인 차례" 입니다.
    //
    //    [모달 폼 - ShowDialog()]
    //      명령 시작 → 폼 열림 → 버튼 클릭 → 폼 닫힘 → 명령 끝
    //      버튼을 누를 때 명령이 아직 안 끝났음 → "애드인 차례" → API 사용 O
    //      (대신 폼이 열려 있는 동안 Revit 을 조작할 수 없습니다)
    //
    //    [모드리스 폼 - Show()]
    //      명령 시작 → 폼 열림 → 명령 끝 (폼은 계속 떠 있음) → 버튼 클릭
    //      버튼을 누를 때 명령이 이미 끝났음 → "애드인 차례" 아님 → API 사용 X
    //      (억지로 사용하면 "... outside of API context is not allowed." 예외 발생)
    //
    //    비유 : 은행 창구
    //      창구 직원(Revit)은 번호표를 뽑고 내 차례가 된 손님만 업무를 봐 줍니다.
    //      ExternalEvent 가 바로 이 "번호표" 입니다.
    //
    //  ■ 해결 방법 : ExternalEvent (번호표)
    //    버튼을 누르면 번호표를 뽑아 두고, 내 차례가 되면 Revit 이 할 일을 실행해 줍니다.
    //
    //    1) ExternalEvent.Create(this)    → 번호표 기계 설치 (폼을 만들 때 한 번)
    //    2) exEvent.Raise()               → 번호표 뽑기 (버튼 클릭 시)
    //    3) Execute(UIApplication app)    → 내 차례! Revit 이 호출해 줌 (API 사용 O)
    //    4) exEvent.Dispose()             → 번호표 기계 철거 (폼을 닫을 때)
    //
    //    ※ 번호표를 뽑는다고 바로 실행되지 않습니다. 차례가 올 때까지 기다립니다.
    //
    //  ■ 흐름
    //    [버튼 클릭] → 번호표 뽑기 → (차례 기다림) → 내 차례 → { } 안의 코드 실행
    //
    //  ■ 사용 방법
    //    버튼 안에 아래 모양을 그대로 쓰고, { } 안에 할 일을 작성하면 됩니다.
    //
    //        RunRevit((uidoc, doc) =>
    //        {
    //            // 할 일
    //        });
    //
    // ═══════════════════════════════════════════════════════════════
    public partial class MainForm : System.Windows.Forms.Form, IExternalEventHandler
    {
        // 번호표 기계
        private readonly ExternalEvent _exEvent;
        // 번호표에 적어 둔 할 일 (내 차례가 되면 실행됨)
        private Action<UIDocument, Document> _action;
        //
        public MainForm()
        {
            InitializeComponent();
            // 번호표 기계 설치
            // (폼은 Command.Execute() 안, 즉 "애드인 차례" 에 만들어지므로 여기서 설치할 수 있습니다)
            _exEvent = ExternalEvent.Create(this);
        }

        // ───────────── 버튼 ─────────────














        // ───────────── 아래는 수정할 필요 없습니다 ─────────────

        /// <summary>
        /// 번호표를 뽑습니다. { } 안의 할 일은 내 차례가 되면 실행됩니다.
        /// </summary>
        private void RunRevit(Action<UIDocument, Document> action)
        {
            // 번호표에 할 일을 적고
            _action = action;
            // 번호표 뽑기 (바로 실행 X → 차례가 되면 Revit 이 Execute() 호출)
            // ※ 차례가 오기 전에 다시 누르면, 마지막에 누른 버튼의 할 일만 실행됩니다.
            _exEvent.Raise();
        }

        /// <summary>
        /// 내 차례! Revit 이 호출해 줍니다. 번호표에 적어 둔 할 일을 실행합니다.
        /// 이 안에서는 Revit API 를 자유롭게 사용할 수 있습니다.
        /// </summary>
        public void Execute(UIApplication app)
        {
            if (_action == null) return;

            UIDocument uidoc = app.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("Modless", "열려 있는 문서가 없습니다.");
                return;
            }

            try
            {
                _action(uidoc, uidoc.Document);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("오류", ex.Message);
            }
            finally
            {
                _action = null;
            }
        }

        /// <summary>
        /// 핸들러 이름. (IExternalEventHandler)
        /// Revit 이 내부적으로 이벤트를 구분할 때 사용합니다. 아무 이름이나 괜찮습니다.
        /// </summary>
        public string GetName()
        {
            return "Modless";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 번호표 기계 철거
            _exEvent.Dispose();
            base.OnFormClosed(e);
        }


        /// <summary>
        /// 바닥생성
        /// </summary>


        public static string m_floorTypeName = "";//전역변수 선언


        private void MainForm_Load(object sender, EventArgs e)
        {
            RunRevit((uidoc, doc) =>
            {
                FilteredElementCollector col = new FilteredElementCollector(doc);
                col.OfCategory(BuiltInCategory.OST_Floors);
                col.OfClass(typeof(FloorType));

                foreach (FloorType item in col)
                {
                    string name = item.Name;
                    comboBox1.Items.Add(name); //comboBox1에 바닥유형 이름을 추가합니다.
                }

            });
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_floorTypeName = comboBox1.SelectedItem.ToString(); //선택한 바닥유형 이름을 전역변수에 저장합니다.
        }
        private void button1_Click(object sender, EventArgs e)
        {
            RunRevit((uidoc, doc) =>
            {
                //바닥을 생성한다.
                FloorType ft = null;
                FilteredElementCollector col = new FilteredElementCollector(doc);
                col.OfClass(typeof(FloorType));

                foreach (FloorType item in col)
                {
                    if (item.Name == m_floorTypeName)
                    {
                        ft = item;
                        break;
                    }
                }

                List<CurveLoop> cls = new List<CurveLoop>();// EdgeLoops를 curveLoop로 변환하여 리스트에 담습니다.

                // Curveloop를 가져오기위해 Face를 선택합니다.
                Reference r = uidoc.Selection.PickObject(ObjectType.Face);
                Element e = doc.GetElement(r);
                GeometryObject go = e.GetGeometryObjectFromReference(r);
                Face face = go as Face;
                EdgeArrayArray eaa = face.EdgeLoops;

                foreach (EdgeArray item in eaa)
                {
                    CurveLoop cl = new CurveLoop();

                    foreach (Edge item1 in item)
                    {
                        cl.Append(item1.AsCurve());
                    }
                    cls.Add(cl);
                }

                // 바닥을 생성합니다.
                using (Transaction trans = new Transaction(doc, "바닥을 생성합니다"))
                {
                    trans.Start();
                    Floor f = Floor.Create(doc, cls, ft.Id, uidoc.ActiveView.GenLevel.Id);

                    Parameter Upparam = f.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);

                }

            });
        }


        //룸객체를 이용하여 바닥 벽 천장을 작성
        private void button2_Click(object sender, EventArgs e)
        {

            RunRevit(static (uidoc, doc) =>
            {
                
                IList<Reference> rf = uidoc.Selection.PickObjects(ObjectType.Element, new RoomSelectionFilter(), "룸을 선택하세요");
                // 사용자가 화면에서 룸을 마우스로 선택하게 합니다

                foreach (Reference item in rf)
                {
                    Room r = (Room)doc.GetElement(item);


                    //3D뷰를 찾는다.
                    FilteredElementCollector col3D = new FilteredElementCollector(doc);
                    col3D.OfClass(typeof(View3D));

                    View3D v3d = null;
                    foreach (View3D v in col3D)
                    {
                        if (v.IsTemplate == false) //템플릿이 아닌 일반3D뷰라면,
                        {
                            v3d = v;
                            break;
                        }
                    }
                    if (v3d == null)
                    {
                        TaskDialog.Show("에러", "3D뷰를 찾을 수 없습니다");
                        return;
                    }
                    ElementCategoryFilter filter = new ElementCategoryFilter(BuiltInCategory.OST_Floors);

                    ReferenceIntersector ri = new ReferenceIntersector(filter, FindReferenceTarget.Element, v3d);
                    // ReferenceIntersector를 이용하여 바닥 슬라브를 찾기 위해 레이저를 쏘는 준비를 합니다.

                    LocationPoint lp = r.Location as LocationPoint;
                    XYZ sp = new XYZ(lp.Point.X, lp.Point.Y, lp.Point.Z + 1200 / 304.8);

                    ReferenceWithContext rwc = ri.FindNearest(sp, -XYZ.BasisZ);
                    //룸의 중심 위쪽에서 아래 방향(-XYZ.BasisZ)으로 보이지 않는 레이저를 쏴서,

                    double hitZ = 0;

                    if (rwc != null)
                    {
                        Reference r1 = rwc.GetReference();
                        XYZ hit = r1.GlobalPoint;

                        hitZ = hit.Z;
                    }
                    //가장 먼저 부딪히는 바닥 슬라브의 높이(hitZ)를 알아냅니다.

                    SpatialElementBoundaryOptions opt = new SpatialElementBoundaryOptions();
                    opt.SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish;

                    IList<IList<BoundarySegment>> loops = r.GetBoundarySegments(opt);
                    // 룸의 경계선을 가져옵니다. (BoundarySegment는 선분, Arc 등으로 구성됩니다.)


                    List<CurveLoop> cls = new List<CurveLoop>(); //룸의 BoundarySegment를 CurveLoop로 변환하여 리스트에 담습니다.
                    foreach (IList<BoundarySegment> loop in loops)
                    {
                        CurveLoop cl = new CurveLoop();
                        foreach (BoundarySegment bs in loop)
                        {
                            Curve c = bs.GetCurve();
                            cl.Append(c);
                        }
                        cls.Add(cl);
                    }

                    //레벨을 가져온다
                    Level findlevel = null;
                    FilteredElementCollector col = new FilteredElementCollector(doc);
                    col.OfClass(typeof(Level));

                  



                }
            });




        


        }

        // Selection 용 필터: 화면에서 Room 요소만 선택 허용
        private class RoomSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Autodesk.Revit.DB.Element elem)
            {
                return elem is Autodesk.Revit.DB.Architecture.Room;
            }

            public bool AllowReference(Autodesk.Revit.DB.Reference reference, Autodesk.Revit.DB.XYZ position)
            {
                // 참조(면/엣지 기반 선택)를 허용하려면 조건을 바꿉니다.
                return false;
            }
        }
    }
}

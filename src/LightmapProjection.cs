using System;

namespace MagmarenaPrivate
{
    internal struct SurfacePoint
    {
        public double X, Y, Z;
        public SurfacePoint(double x,double y,double z) { X=x;Y=y;Z=z; }
        public static SurfacePoint operator -(SurfacePoint a,SurfacePoint b) { return new SurfacePoint(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static SurfacePoint operator +(SurfacePoint a,SurfacePoint b) { return new SurfacePoint(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static SurfacePoint operator *(SurfacePoint a,double t) { return new SurfacePoint(a.X*t,a.Y*t,a.Z*t); }
        public static double Dot(SurfacePoint a,SurfacePoint b) { return a.X*b.X+a.Y*b.Y+a.Z*b.Z; }
    }

    internal static class LightmapProjection
    {
        public static SurfacePoint Closest(SurfacePoint p,SurfacePoint a,SurfacePoint b,SurfacePoint c,out SurfacePoint weights)
        {
            SurfacePoint ab=b-a,ac=c-a,ap=p-a;
            double d1=SurfacePoint.Dot(ab,ap),d2=SurfacePoint.Dot(ac,ap);
            if (d1<=0 && d2<=0) { weights=new SurfacePoint(1,0,0);return a; }
            SurfacePoint bp=p-b;
            double d3=SurfacePoint.Dot(ab,bp),d4=SurfacePoint.Dot(ac,bp);
            if (d3>=0 && d4<=d3) { weights=new SurfacePoint(0,1,0);return b; }
            double vc=d1*d4-d3*d2;
            if (vc<=0 && d1>=0 && d3<=0)
            { double t=d1/(d1-d3);weights=new SurfacePoint(1-t,t,0);return a+ab*t; }
            SurfacePoint cp=p-c;
            double d5=SurfacePoint.Dot(ab,cp),d6=SurfacePoint.Dot(ac,cp);
            if (d6>=0 && d5<=d6) { weights=new SurfacePoint(0,0,1);return c; }
            double vb=d5*d2-d1*d6;
            if (vb<=0 && d2>=0 && d6<=0)
            { double t=d2/(d2-d6);weights=new SurfacePoint(1-t,0,t);return a+ac*t; }
            double va=d3*d6-d5*d4;
            if (va<=0 && d4-d3>=0 && d5-d6>=0)
            { double t=(d4-d3)/((d4-d3)+(d5-d6));weights=new SurfacePoint(0,1-t,t);return b+(c-b)*t; }
            double inverse=1/(va+vb+vc),v=vb*inverse,w=vc*inverse;
            weights=new SurfacePoint(1-v-w,v,w);
            return a+ab*v+ac*w;
        }
    }
}

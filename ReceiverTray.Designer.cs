namespace tagless_receiver;

partial class ReceiverTray
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        lblReceiverId = new Label();
        btnRegister = new Button();
        SuspendLayout();
        // 
        // lblReceiverId
        // 
        lblReceiverId.AutoSize = true;
        lblReceiverId.Location = new Point(142, 211);
        lblReceiverId.Name = "lblReceiverId";
        lblReceiverId.Size = new Size(107, 15);
        lblReceiverId.TabIndex = 1;
        lblReceiverId.Text = "Receiver Device ID";
        // 
        // btnRegister
        // 
        btnRegister.Location = new Point(142, 356);
        btnRegister.Name = "btnRegister";
        btnRegister.Size = new Size(75, 23);
        btnRegister.TabIndex = 2;
        btnRegister.Text = "등록";
        btnRegister.UseVisualStyleBackColor = true;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(480, 720);
        Controls.Add(btnRegister);
        Controls.Add(lblReceiverId);
        Name = "Form1";
        Text = "Tagless Receiver";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label label1;
    private Label lblReceiverId;
    private Button btnRegister;
}
